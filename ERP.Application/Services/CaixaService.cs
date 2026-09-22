using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class CaixaService : ICaixaService
{
    private readonly IUnitOfWork _uow;

    // S{N} FIX — achado ao mover o Resumo de Caixa pro servidor (Fase C):
    // {valor:N2} sozinho usa CultureInfo.CurrentCulture do PROCESSO. No WPF
    // isso sempre foi pt-BR (Windows configurado em português no PC da
    // loja) — "R$ 20,00". Rodando na API (Azure/Linux), a cultura padrão do
    // processo pode não ser pt-BR, e a mesma formatação viraria
    // silenciosamente "R$ 20.00" (ponto em vez de vírgula) sem nenhum erro,
    // só um extrato com formatação errada pro operador brasileiro. Fixado
    // explicitamente aqui, não depende de como o host está configurado.
    private static readonly CultureInfo CulturaMoeda = CultureInfo.GetCultureInfo("pt-BR");
    private static string Moeda(decimal valor) => valor.ToString("N2", CulturaMoeda);

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
            DataAbertura  = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil(),
            ValorAbertura = dto.ValorAbertura,
            Status        = StatusCaixa.Aberto
        };

        novoCaixa.Movimentos.Add(new CaixaMovimento
        {
            Tipo      = TipoMovimentoCaixa.Abertura,
            Descricao = "TROCO INICIAL (ABERTURA)",
            Valor     = dto.ValorAbertura,
            DataHora  = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil()
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
                                               decimal maxSangriaValue = 0m, Guid? vendaId = null, Guid? salePaymentId = null)
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
            DataHora       = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil(),
            VendaId        = vendaId,
            SalePaymentId  = salePaymentId
        };

        await _uow.Caixas.AddMovimentoAsync(novoMovimento);
        await _uow.CommitAsync();
    }

    // 🟢 Fecha o caixa DO USUÁRIO
    public async Task<bool> ExisteMovimentoParaSalePaymentAsync(Guid salePaymentId)
        => await _uow.Caixas.ExisteMovimentoParaSalePaymentAsync(salePaymentId);

    public async Task FecharCaixaAsync(Guid usuarioId)
    {
        var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId);

        if (caixaAberto != null)
        {
            caixaAberto.Status = StatusCaixa.Fechado;
            caixaAberto.DataFechamento = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();

            // S{N} FIX — achado auditando pra Fase C (módulo Caixa): antes,
            // fechar o caixa por AQUI (endpoint da API) fazia a coisa certa
            // com Status/DataFechamento, mas nunca deixava rastro no
            // extrato. Enquanto isso, a tela de Resumo do WPF
            // (ResumoCaixaViewModel.Encerrar) tinha o caminho OPOSTO: criava
            // o movimento "FECHAMENTO DE CAIXA" certinho, só que fechava o
            // caixa direto no banco (bypass deste método) SEM setar
            // DataFechamento — todo caixa fechado por aquela tela ficava com
            // DataFechamento eternamente nulo. Unificado aqui: agora este é
            // o ÚNICO lugar que fecha caixa, faz as duas coisas certas de
            // uma vez, e o WPF (Fase C) passa a chamar só isto.
            await _uow.Caixas.AddMovimentoAsync(new CaixaMovimento
            {
                Id             = Guid.NewGuid(),
                CaixaId        = caixaAberto.Id,
                Valor          = 0,
                Descricao      = "FECHAMENTO DE CAIXA",
                FormaPagamento = PaymentMethod.Dinheiro,
                Tipo           = TipoMovimentoCaixa.Fechamento,
                DataHora       = caixaAberto.DataFechamento.Value
            });

            _uow.Caixas.Update(caixaAberto);
            await _uow.CommitAsync();
        }
    }

    /// <summary>
    /// Fase C, módulo Caixa — porta fiel de
    /// ResumoCaixaViewModel.CarregarResumoAsync (WPF), com duas limpezas:
    /// (1) tira a reflexão que buscava Descricao/Observacao/Motivo/Historico
    /// via GetType().GetProperty — CaixaMovimento só tem Descricao, as outras
    /// três nunca existiram, a reflexão nunca fazia nada além de achar
    /// Descricao do jeito mais lento possível; (2) tira a reflexão que lia
    /// UsuarioId de Caixa do mesmo jeito — Caixa.UsuarioId é uma propriedade
    /// normal, sempre existiu. Fora essas duas limpezas, o comportamento é
    /// idêntico ao original, incluindo os fixes S17 (PagamentoDespesa) e do
    /// CancelamentoVenda que já estavam no WPF.
    /// </summary>
    public async Task<ResumoCaixaDto?> ObterResumoAsync(Guid usuarioId, DateTime data)
    {
        var caixa = await _uow.Caixas.ObterCaixaPorDataEUsuarioAsync(data, usuarioId);
        if (caixa == null) return null;

        var dto = new ResumoCaixaDto
        {
            CaixaId        = caixa.Id,
            NumeroCaixa    = caixa.NumeroCaixa,
            OperadorNome   = caixa.OperadorNome,
            DataAbertura   = caixa.DataAbertura,
            DataFechamento = caixa.DataFechamento,
            Status         = caixa.Status,
        };

        foreach (var mov in caixa.Movimentos.OrderBy(m => m.DataHora))
        {
            var textoDescricao = mov.Descricao ?? string.Empty;
            bool isEstorno = textoDescricao.ToLower().Contains("estorno");

            if (mov.Tipo == TipoMovimentoCaixa.Abertura)
            {
                dto.SaldoInicial += mov.Valor;
                dto.Extrato.Add($"ABERTURA \t\t\t + R$ {Moeda(mov.Valor)}");
            }
            else if (mov.Tipo == TipoMovimentoCaixa.Venda || mov.Tipo.ToString() == "RecebimentoConta")
            {
                if (mov.FormaPagamento == PaymentMethod.Dinheiro) dto.VendasDinheiro += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.Pix) dto.VendasPix += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.CartaoDebito) dto.VendasCartaoDebito += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.CartaoCredito) dto.VendasCartaoCredito += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.Haver) dto.VendasHaver += mov.Valor;
                else dto.VendasAPrazo += mov.Valor;

                string prefixoExtrato = textoDescricao.ToUpper().Contains("FIADO") || mov.Tipo.ToString() == "RecebimentoConta"
                                    ? "REC. FIADO"
                                    : "VENDA";

                dto.Extrato.Add($"{prefixoExtrato} ({mov.FormaPagamento}) \t + R$ {Moeda(mov.Valor)}");
            }
            else if (mov.Tipo == TipoMovimentoCaixa.Suprimento)
            {
                dto.Suprimentos += mov.Valor;
                dto.Extrato.Add($"SUPRIMENTO\t\t\t + R$ {Moeda(mov.Valor)}");
            }
            else if (mov.Tipo == TipoMovimentoCaixa.Sangria)
            {
                if (isEstorno)
                {
                    if (mov.FormaPagamento == PaymentMethod.Dinheiro) dto.VendasDinheiro -= mov.Valor;
                    else if (mov.FormaPagamento == PaymentMethod.Pix) dto.VendasPix -= mov.Valor;
                    else if (mov.FormaPagamento == PaymentMethod.CartaoDebito) dto.VendasCartaoDebito -= mov.Valor;
                    else if (mov.FormaPagamento == PaymentMethod.CartaoCredito) dto.VendasCartaoCredito -= mov.Valor;
                    else if (mov.FormaPagamento == PaymentMethod.Haver) dto.VendasHaver -= mov.Valor;
                    else dto.VendasAPrazo -= mov.Valor;

                    dto.Extrato.Add($"ESTORNO ({mov.FormaPagamento})\t\t - R$ {Moeda(mov.Valor)}");
                }
                else
                {
                    if (mov.FormaPagamento == PaymentMethod.Dinheiro) dto.Sangrias += mov.Valor;
                    dto.Extrato.Add($"SANGRIA \t\t\t - R$ {Moeda(mov.Valor)}");
                }
            }
            else if (mov.Tipo == TipoMovimentoCaixa.PagamentoDespesa)
            {
                // mov.Valor já vem negativo daqui (RegistrarMovimentoAsync recebe
                // -conta.Valor em ContaPagarViewModel) — usa Math.Abs pra exibir e
                // somar como valor positivo de saída, igual às outras categorias.
                dto.Despesas += Math.Abs(mov.Valor);
                dto.Extrato.Add($"{textoDescricao}\t\t - R$ {Moeda(Math.Abs(mov.Valor))}");
            }
            else if (mov.Tipo == TipoMovimentoCaixa.CancelamentoVenda)
            {
                // cancelar venda já cria o estorno certo no banco
                // (SaleService.CancelAsync, valor negativo). Usa += (não -=)
                // porque mov.Valor aqui JÁ vem negativo — subtrair de novo
                // inverteria o sinal errado.
                if (mov.FormaPagamento == PaymentMethod.Dinheiro) dto.VendasDinheiro += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.Pix) dto.VendasPix += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.CartaoDebito) dto.VendasCartaoDebito += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.CartaoCredito) dto.VendasCartaoCredito += mov.Valor;
                else if (mov.FormaPagamento == PaymentMethod.Haver) dto.VendasHaver += mov.Valor;
                else dto.VendasAPrazo += mov.Valor;

                dto.Extrato.Add($"ESTORNO ({mov.FormaPagamento})\t\t - R$ {Moeda(Math.Abs(mov.Valor))}");
            }
            else if (!string.IsNullOrWhiteSpace(textoDescricao))
            {
                // Defesa: qualquer TipoMovimentoCaixa futuro sem branch dedicado
                // ainda aparece no extrato, em vez de sumir silenciosamente
                // (era exatamente isso que causava o bug do PagamentoDespesa).
                dto.Extrato.Add($"{textoDescricao}\t\t {(mov.Valor >= 0 ? "+" : "-")} R$ {Moeda(Math.Abs(mov.Valor))}");
            }
        }

        return dto;
    }
}