// ── ERP.Domain/Entities/OperadoraRecebimento.cs ───────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Uma operadora de cartão/recebimento (Stone, Cielo, Mercado Pago, etc.) que o
/// tenant usa. Guarda os prazos de liquidação PRÓPRIOS dessa operadora — não do
/// tenant, porque o mesmo tenant pode ter mais de uma operadora, cada uma com
/// prazo diferente. Preparação de base (Categoria B) para o futuro conceito de
/// Recebíveis de Operadora — hoje só cadastro, ainda sem nenhum fluxo real
/// consumindo isso (Cartão continua só no CaixaMovimento por enquanto).
/// </summary>
public class OperadoraRecebimento : BaseEntity
{
    public string Nome { get; set; } = string.Empty; // "Stone", "Cielo", "Mercado Pago"

    public int PrazoDebitoDias           { get; set; } = 1;
    public int PrazoCreditoVistaDias     { get; set; } = 1;
    public int PrazoCreditoParceladoDias { get; set; } = 30;

    public bool AntecipacaoAutomatica { get; set; } = false;

    /// <summary>Taxa percentual descontada pela operadora (ex: 2.5 = 2,5%). Preparação
    /// pra quando Recebíveis precisar calcular o valor líquido depositado.</summary>
    public decimal TaxaDebitoPercentual           { get; set; } = 0m;
    public decimal TaxaCreditoVistaPercentual     { get; set; } = 0m;
    public decimal TaxaCreditoParceladoPercentual { get; set; } = 0m;

    /// <summary>Conta bancária onde essa operadora deposita — usado quando Recebíveis existir.</summary>
    public Guid?          ContaDestinoId { get; set; }
    public ContaBancaria? ContaDestino   { get; set; }

    public bool IsAtiva { get; set; } = true;

    /// <summary>
    /// Marca qual operadora processa automaticamente os cartões do PDV (mesmo
    /// padrão do ContaBancaria.ContaPadrao pro PIX). Só uma operadora padrão por
    /// vez. Loja com máquinas diferentes por bandeira/tipo fica pra quando isso
    /// aparecer como necessidade real — hoje resolve os ~90% dos casos de uma
    /// loja pequena com uma maquininha só.
    /// </summary>
    public bool OperadoraPadrao { get; set; } = false;

    /// <summary>
    /// Quem sabe calcular taxa/prazo dessa operadora é a própria operadora — não
    /// o service que a chama (evita Modelo de Domínio Anêmico). Se um dia Stone
    /// e Cielo tiverem regras diferentes de arredondamento ou exceção, o lugar
    /// certo pra essa diferença é aqui dentro, não espalhado em quem consome.
    /// </summary>
    public (decimal ValorTaxa, decimal ValorLiquido, DateTime DataPrevistaLiquidacao) CalcularRecebimento(
        FormaRecebimentoOperadora forma, decimal valorBruto, DateTime dataVenda)
    {
        var (taxaPercentual, prazoDias) = forma switch
        {
            FormaRecebimentoOperadora.Debito           => (TaxaDebitoPercentual, PrazoDebitoDias),
            FormaRecebimentoOperadora.CreditoVista      => (TaxaCreditoVistaPercentual, PrazoCreditoVistaDias),
            FormaRecebimentoOperadora.CreditoParcelado  => (TaxaCreditoParceladoPercentual, PrazoCreditoParceladoDias),
            _ => throw new ArgumentOutOfRangeException(nameof(forma), forma, "Forma de recebimento desconhecida.")
        };

        var valorTaxa    = Math.Round(valorBruto * (taxaPercentual / 100m), 2);
        var valorLiquido = valorBruto - valorTaxa;
        var dataPrevista = dataVenda.AddDays(prazoDias);

        return (valorTaxa, valorLiquido, dataPrevista);
    }
}