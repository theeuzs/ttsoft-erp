// ── ERP.Application/DTOs/ContaBancariaDtos.cs ─────────────────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

public class ContaBancariaDto
{
    public Guid    Id           { get; set; }
    public string  Apelido      { get; set; } = string.Empty;
    public string  Banco        { get; set; } = string.Empty;
    public string  Agencia      { get; set; } = string.Empty;
    public string  NumeroConta  { get; set; } = string.Empty;
    public decimal SaldoInicial { get; set; }
    public decimal SaldoAtual   { get; set; }
    public bool    IsAtiva      { get; set; } = true;
    public bool    ContaPadrao  { get; set; } = false;
}

public class CriarContaBancariaDto
{
    public string  Apelido      { get; set; } = string.Empty;
    public string  Banco        { get; set; } = string.Empty;
    public string  Agencia      { get; set; } = string.Empty;
    public string  NumeroConta  { get; set; } = string.Empty;
    public decimal SaldoInicial { get; set; }
}

public class MovimentoContaBancariaDto
{
    public Guid                       Id        { get; set; }
    public DateTime                   DataHora  { get; set; }
    public TipoMovimentoContaBancaria Tipo      { get; set; }
    public string                     Descricao { get; set; } = string.Empty;
    public decimal                    Valor     { get; set; }
    public OrigemMovimentoFinanceiro  OrigemTipo { get; set; }
    public Guid?                      OrigemId   { get; set; }
}

public record SaldoContaBancariaResumoDto(Guid ContaBancariaId, string Apelido, decimal Saldo);

/// <summary>
/// Saldo consolidado — soma de todos os Caixas abertos no momento + todas as
/// Contas Bancárias ativas. É a "visão de dinheiro total da loja agora".
/// </summary>
public record PosicaoFinanceiraDto(
    decimal SaldoTotalCaixasAbertos,
    IReadOnlyList<SaldoContaBancariaResumoDto> ContasBancarias,
    decimal SaldoTotalContasBancarias,
    decimal SaldoConsolidado);

// ── Conciliação Bancária ──────────────────────────────────────────────────────
/// <summary>Uma linha do extrato importado (OFX), com sugestão de match (se houver).</summary>
public class SugestaoConciliacaoDto
{
    public string   FitId               { get; set; } = string.Empty;
    public DateTime Data                { get; set; }
    public decimal  Valor               { get; set; } // negativo = saída, positivo = entrada, igual ao OFX
    public string   Descricao           { get; set; } = string.Empty;

    /// <summary>Preenchido só quando existe exatamente um candidato — ambíguo (0 ou 2+) fica null.</summary>
    public Guid?    MovimentoSugeridoId          { get; set; }
    public string?  MovimentoSugeridoDescricao   { get; set; }
    public DateTime? MovimentoSugeridoData       { get; set; }

    /// <summary>Quantos lançamentos já existentes batem com valor/tipo/janela de data — 0, 1 ou mais de 1.</summary>
    public int      QuantidadeCandidatos { get; set; }

    public bool TemSugestao  => MovimentoSugeridoId.HasValue;
    public bool SemSugestao  => !MovimentoSugeridoId.HasValue;
}