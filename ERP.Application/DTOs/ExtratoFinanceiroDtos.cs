// ── ERP.Application/DTOs/ExtratoFinanceiroDtos.cs ─────────────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

/// <summary>
/// Uma linha do Extrato Financeiro unificado — Caixa, Conta Bancária e
/// Recebíveis de Operadora, todos numa timeline só. Não é regra de negócio
/// nova: é consulta sobre o que já existe (Categoria A "quase de graça").
/// </summary>
public class ExtratoItemDto
{
    public DateTime DataHora  { get; set; }
    public string   Origem   { get; set; } = string.Empty; // "Caixa" | "Banco — {Apelido}" | "Recebível — {Operadora}"
    public string   Tipo     { get; set; } = string.Empty; // "Entrada" | "Saída" | "Pendente"
    public string   Descricao { get; set; } = string.Empty;
    public decimal  Valor    { get; set; }

    public OrigemMovimentoFinanceiro OrigemTipo { get; set; } = OrigemMovimentoFinanceiro.Manual;
    public Guid?    OrigemId { get; set; }
}
