using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class CaixaService : ICaixaService
{
    private readonly IUnitOfWork _uow;

    // S8 FIX: new Random() sem seed → colisão de NumeroCaixa em chamadas concorrentes (seed = TickCount ~15ms).
    private static readonly ThreadLocal<Random> _rng =
        new(() => new Random(Guid.NewGuid().GetHashCode()));

    public CaixaService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    // 🟢 Agora busca o caixa DO USUÁRIO, não qualquer caixa aberto
    public async Task<CaixaDto?> ObterCaixaAbertoAsync(Guid usuarioId)
    {
        var caixa = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId);
        if (caixa == null) return null;

        return new CaixaDto
        {
            Id = caixa.Id,
            NumeroCaixa = caixa.NumeroCaixa,
            OperadorNome = caixa.OperadorNome,
            DataAbertura = caixa.DataAbertura,
            ValorAbertura = caixa.ValorAbertura,
            Status = caixa.Status
        };
    }

    public async Task AbrirCaixaAsync(AbrirCaixaDto dto)
    {
        // Verificação rápida no código (evita round-trip desnecessário ao banco na maioria dos casos)
        var caixaExistente = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(dto.UsuarioId);
        if (caixaExistente != null)
            throw new InvalidOperationException("Você já tem um caixa aberto!");

        var novoCaixa = new Caixa
        {
            NumeroCaixa   = _rng.Value!.Next(100, 999),
            UsuarioId     = dto.UsuarioId,
            OperadorNome  = dto.OperadorNome ?? "Operador",
            DataAbertura  = DateTime.Now,
            ValorAbertura = dto.ValorAbertura,
            Status        = StatusCaixa.Aberto
        };

        novoCaixa.Movimentos.Add(new CaixaMovimento
        {
            Tipo      = TipoMovimentoCaixa.Abertura,
            Descricao = "TROCO INICIAL (ABERTURA)",
            Valor     = dto.ValorAbertura,
            DataHora  = DateTime.Now
        });

        await _uow.Caixas.AddAsync(novoCaixa);

        try
        {
            await _uow.CommitAsync();
        }
        catch (Exception ex)
            when (ex.InnerException?.Message.Contains("IX_Caixa_UsuarioTenantAberto") == true
               || ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            // S9: unique partial index no banco captura TOCTOU — dois cliques simultâneos no
            // botão "Abrir Caixa" que passam pelo check em código chegam aqui com erro amigável.
            // Não referenciamos DbUpdateException diretamente (ERP.Application não depende de EF Core).
            throw new InvalidOperationException("Você já tem um caixa aberto!", ex);
        }
    }

    // 🟢 Registra movimento no caixa DO USUÁRIO
    public async Task RegistrarMovimentoAsync(Guid usuarioId, decimal valor, string descricao,
                                               PaymentMethod formaPagamento, TipoMovimentoCaixa tipo,
                                               decimal maxSangriaValue = 0m)
    {
        // S8 FIX: silêncio com caixa fechado → 200 OK fantasma; agora lança exceção (400 no controller).
        var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)
            ?? throw new InvalidOperationException("Nenhum caixa aberto para este usuário.");

        // S13: validação de sangria via SangriaPolicy (antes inline — S8).
        // Inclui: valor > 0, valor <= saldo, valor <= limite do cargo.
        if (tipo == TipoMovimentoCaixa.Sangria)
        {
            var saldoDinheiro = await _uow.Caixas.GetSaldoDinheiroAsync(caixaAberto.Id);
            var (sangriaOk, sangriaErro) = ERP.Application.Helpers.SangriaPolicy.Validar(
                valor, saldoDinheiro, maxSangriaValue);
            if (!sangriaOk) throw new InvalidOperationException(sangriaErro!);
        }

        var novoMovimento = new CaixaMovimento
        {
            Id             = Guid.NewGuid(),
            CaixaId        = caixaAberto.Id,
            Valor          = valor,
            Descricao      = descricao,
            FormaPagamento = formaPagamento,
            Tipo           = tipo,
            DataHora       = DateTime.Now
        };

        await _uow.Caixas.AddMovimentoAsync(novoMovimento);
        await _uow.CommitAsync();
    }

    // 🟢 Fecha o caixa DO USUÁRIO
    public async Task FecharCaixaAsync(Guid usuarioId)
    {
        var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId);
        
        if (caixaAberto != null)
        {
            caixaAberto.Status = StatusCaixa.Fechado;
            caixaAberto.DataFechamento = DateTime.Now;

            _uow.Caixas.Update(caixaAberto);
            await _uow.CommitAsync();
        }
    }
}