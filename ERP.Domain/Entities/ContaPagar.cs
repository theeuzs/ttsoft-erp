using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class ContaPagar : BaseEntity
{
    // Para quem estamos devendo ou o que é (Ex: "Fornecedor Votorantim", "Conta de Luz")
    public string Descricao { get; set; } = string.Empty;

    // O valor
    public decimal Valor { get; set; }

    // Categoria para os gráficos (Ex: "Fornecedor", "Imposto", "Despesa Fixa")
    public string Categoria { get; set; } = string.Empty;

    public DateTime DataEmissao { get; set; } = DateTime.Now;
    public DateTime DataVencimento { get; set; }
    public DateTime? DataPagamento { get; set; }

    // "Pendente" ou "Pago"
    public string Status { get; set; } = "Pendente";
}