using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Registro de entrega vinculado a uma venda.
/// Um pedido pode gerar 0 ou 1 entrega (entregas no balcão não geram registro).
/// </summary>
public class Entrega : BaseEntity
{
    // ── Venda de origem ───────────────────────────────────────────────────────
    public Guid   SaleId     { get; set; }
    public Sale?  Sale       { get; set; }

    // ── Cliente / destino ─────────────────────────────────────────────────────
    /// <summary>ID do cliente (para lookup de histórico).</summary>
    public Guid?  CustomerId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;

    // Endereço de entrega — pré-preenchido pelo endereço do cliente, editável
    public string? Logradouro   { get; set; }
    public string? Numero       { get; set; }
    public string? Complemento  { get; set; }
    public string? Bairro       { get; set; }
    public string? Cidade       { get; set; }
    public string? UF           { get; set; }
    public string? CEP          { get; set; }
    /// <summary>Ponto de referência livre ("casa amarela, portão de ferro").</summary>
    public string? Referencia   { get; set; }

    // ── Agendamento ───────────────────────────────────────────────────────────
    public DateTime  DataPrevista { get; set; } = DateTime.Today.AddDays(1);
    public DateTime? DataEntrega  { get; set; }  // preenchida ao confirmar entrega

    // Janela de horário preferida pelo cliente
    public string? JanelaHorario { get; set; } // "08:00-12:00", "14:00-18:00", etc.

    // ── Operação ──────────────────────────────────────────────────────────────
    public StatusEntrega Status { get; set; } = StatusEntrega.Pendente;

    public Guid?  MotoristaId   { get; set; }
    public User?  Motorista     { get; set; }
    public string? MotoristaNome { get; set; } // snapshot para histórico

    public Guid?    VeiculoId { get; set; }
    public Veiculo? Veiculo   { get; set; }

    public string? Observacoes      { get; set; }
    public string? MotivoProblema   { get; set; }  // preenchido em Cancelada/Reagendada

    // ── Confirmação ───────────────────────────────────────────────────────────
    /// <summary>Nome de quem assinou o recebimento.</summary>
    public string? AssinadoPor     { get; set; }
    /// <summary>URL da foto de comprovante (Azure Blob).</summary>
    public string? FotoComprovante { get; set; }

    // ── Custo ─────────────────────────────────────────────────────────────────
    public decimal CustoEntrega { get; set; } = 0;
}
