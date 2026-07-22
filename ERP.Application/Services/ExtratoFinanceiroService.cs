// ── ERP.Application/Services/ExtratoFinanceiroService.cs ──────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using System.Linq;

namespace ERP.Application.Services;

public class ExtratoFinanceiroService : IExtratoFinanceiroService
{
    private readonly IUnitOfWork _uow;
    public ExtratoFinanceiroService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<ExtratoItemDto>> ObterExtratoAsync(DateTime inicio, DateTime fim)
    {
        // Inclui o dia inteiro do "fim" (a tela normalmente passa só a data, sem hora).
        var fimAjustado = fim.Date.AddDays(1).AddTicks(-1);

        var itens = new List<ExtratoItemDto>();

        // ── Caixa ─────────────────────────────────────────────────────────────
        var movimentosCaixa = await _uow.Caixas.GetMovimentosPorPeriodoAsync(inicio.Date, fimAjustado);
        foreach (var m in movimentosCaixa)
        {
            // Fechamento é só o encerramento da sessão, sem dinheiro se movendo —
            // não faz sentido numa timeline de "o que aconteceu com o dinheiro".
            if (m.Tipo == TipoMovimentoCaixa.Fechamento) continue;

            var tipo = m.Tipo is TipoMovimentoCaixa.Sangria or TipoMovimentoCaixa.PagamentoDespesa or TipoMovimentoCaixa.CancelamentoVenda
                ? "Saída"
                : "Entrada";

            itens.Add(new ExtratoItemDto
            {
                DataHora  = m.DataHora,
                Origem    = "Caixa",
                Tipo      = tipo,
                Descricao = m.Descricao,
                Valor     = m.Valor
            });
        }

        // ── Conta Bancária ────────────────────────────────────────────────────
        var movimentosBanco = await _uow.ContasBancarias.GetMovimentosPorPeriodoAsync(inicio.Date, fimAjustado);
        foreach (var m in movimentosBanco)
        {
            itens.Add(new ExtratoItemDto
            {
                DataHora   = m.DataHora,
                Origem     = $"Banco — {m.ContaBancaria?.Apelido ?? "?"}",
                Tipo       = m.Tipo == TipoMovimentoContaBancaria.Entrada ? "Entrada" : "Saída",
                Descricao  = m.Descricao,
                Valor      = m.Valor,
                OrigemTipo = m.OrigemTipo,
                OrigemId   = m.OrigemId
            });
        }

        // ── Recebíveis de Operadora (a criação, não a liquidação — essa já
        // aparece como entrada de Banco acima, com OrigemTipo=LiquidacaoOperadora) ──
        var recebiveis = await _uow.RecebiveisOperadora.GetPorPeriodoAsync(inicio.Date, fimAjustado);
        foreach (var r in recebiveis)
        {
            // S17 FIX: recebível NUNCA deve virar "Entrada" real, nem antes nem
            // depois de liquidado — senão o mesmo dinheiro é contado duas vezes:
            // uma vez aqui (a venda em cartão acontecendo) e outra na Liquidação
            // (o depósito de verdade chegando no banco). Recebível é sempre
            // informativo: mostra a jornada, não soma no total de Entradas.
            var tipoLabel = r.Status == StatusRecebivel.Pendente
                ? "Recebível Pendente"
                : "Recebível Liquidado";

            var descricao = r.Status == StatusRecebivel.Liquidado
                ? $"Venda em {r.FormaRecebimento} — já liquidado em {r.DataLiquidacao:dd/MM/yyyy} (ver entrada em Banco)"
                : $"Venda em {r.FormaRecebimento} — líquido previsto {r.ValorLiquido:C2}";

            itens.Add(new ExtratoItemDto
            {
                DataHora   = r.DataVenda,
                Origem     = $"Recebível — {r.OperadoraRecebimento?.Nome ?? "?"}",
                Tipo       = tipoLabel,
                Descricao  = descricao,
                Valor      = r.ValorLiquido,
                OrigemTipo = OrigemMovimentoFinanceiro.Venda,
                OrigemId   = r.VendaId
            });
        }

        return itens.OrderByDescending(i => i.DataHora).ToList();
    }
}