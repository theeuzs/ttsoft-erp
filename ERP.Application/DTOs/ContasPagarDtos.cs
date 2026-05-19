// ── ERP.Application/DTOs/ContasPagarDtos.cs ──────────────────────────────────
namespace ERP.Application.DTOs;

/// <summary>Payload de entrada para criar uma conta a pagar.</summary>
public class CreateContaPagarDto
{
    public string   Descricao      { get; set; } = string.Empty;
    public decimal  Valor          { get; set; }
    public DateTime DataVencimento { get; set; }
    public string?  Categoria      { get; set; }
}

/// <summary>Conta a pagar serializada para a API.</summary>
public class ContaPagarDto
{
    public Guid      Id             { get; init; }
    public string    Descricao      { get; init; } = string.Empty;
    public decimal   Valor          { get; init; }
    public string    Categoria      { get; init; } = string.Empty;
    public DateTime  DataVencimento { get; init; }
    public DateTime? DataPagamento  { get; init; }
    public string    Status         { get; init; } = string.Empty;
    public bool      Vencida        => Status == "Pendente" && DataVencimento.Date < DateTime.Today;
}

/// <summary>Resumo financeiro agregado de contas a pagar.</summary>
public class ContaPagarResumoDto
{
    public decimal TotalPendente { get; init; }
    public decimal TotalVencido  { get; init; }
    public int     QtdContas     { get; init; }
    public int     QtdVencidas   { get; init; }
}
