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
        // 🟢 Verifica se ESTE USUÁRIO já tem caixa aberto (não qualquer um)
        var caixaExistente = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(dto.UsuarioId);
        if (caixaExistente != null)
            throw new InvalidOperationException("Você já tem um caixa aberto!");

        var novoCaixa = new Caixa
        {
            NumeroCaixa = _rng.Value!.Next(100, 999),
            UsuarioId = dto.UsuarioId,
            OperadorNome = dto.OperadorNome ?? "Operador",  // 🟢 Agora vem do DTO
            DataAbertura = DateTime.Now,
            ValorAbertura = dto.ValorAbertura,
            Status = StatusCaixa.Aberto
        };

        novoCaixa.Movimentos.Add(new CaixaMovimento
        {
            Tipo = TipoMovimentoCaixa.Abertura,
            Descricao = "TROCO INICIAL (ABERTURA)",
            Valor = dto.ValorAbertura,
            DataHora = DateTime.Now
        });

        await _uow.Caixas.AddAsync(novoCaixa);
        await _uow.CommitAsync();
    }

    // 🟢 Registra movimento no caixa DO USUÁRIO
    public async Task RegistrarMovimentoAsync(Guid usuarioId, decimal valor, string descricao, 
                                               PaymentMethod formaPagamento, TipoMovimentoCaixa tipo)
    {
        // S8 FIX: silêncio com caixa fechado → 200 OK fantasma; agora lança exceção (400 no controller).
        var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)
            ?? throw new InvalidOperationException("Nenhum caixa aberto para este usuário.");

        // S8 FIX: sangria não pode exceder saldo em dinheiro (integridade contábil).
        if (tipo == TipoMovimentoCaixa.Sangria)
        {
            var saldoDinheiro = await _uow.Caixas.GetSaldoDinheiroAsync(caixaAberto.Id);
            if (valor > saldoDinheiro)
                throw new InvalidOperationException(
                    $"Sangria de R$ {valor:F2} excede o saldo em dinheiro do caixa: R$ {saldoDinheiro:F2}.");
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