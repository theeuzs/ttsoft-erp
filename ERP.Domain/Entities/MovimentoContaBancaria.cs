// ── ERP.Domain/Entities/MovimentoContaBancaria.cs ─────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Lançamento manual (entrada/saída) numa ContaBancaria. Mesma forma de CaixaMovimento
/// de propósito — os dois são consumidos juntos pelo relatório de Balanço, mas vivem em
/// tabelas separadas (ver comentário em ContaBancaria.cs).
/// </summary>
public class MovimentoContaBancaria : BaseEntity
{
    public Guid           ContaBancariaId { get; set; }
    public ContaBancaria? ContaBancaria   { get; set; }

    public DateTime                     DataHora  { get; set; } = DateTime.Now;
    public TipoMovimentoContaBancaria    Tipo      { get; set; }
    public string                       Descricao { get; set; } = string.Empty;
    public decimal                      Valor     { get; set; }

    /// <summary>
    /// Conciliação Bancária: true quando esse lançamento já foi confirmado
    /// contra uma linha do extrato bancário (OFX) importado.
    /// </summary>
    public bool Conciliado { get; set; } = false;

    /// <summary>
    /// Identificador único da transação no extrato OFX (FITID) — preenchido só
    /// quando esse movimento nasceu de, ou foi confirmado contra, uma linha de
    /// extrato importada. Evita conciliar a mesma linha duas vezes se o mesmo
    /// arquivo (ou um período sobreposto) for importado de novo.
    /// </summary>
    public string? FitId { get; set; }

    /// <summary>
    /// De onde esse movimento nasceu — permite rastreabilidade (clicar e abrir
    /// a venda/conta/liquidação exata). OrigemId fica null quando a origem é um
    /// lote (ex: liquidação de vários recebíveis de uma vez) sem um único dono.
    /// </summary>
    public OrigemMovimentoFinanceiro OrigemTipo { get; set; } = OrigemMovimentoFinanceiro.Manual;
    public Guid? OrigemId { get; set; }
}