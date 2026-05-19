using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Veículo da frota da loja (caminhão, moto, van, etc.).
/// Vinculado às entregas para rastreamento por veículo.
/// </summary>
public class Veiculo : BaseEntity
{
    public string Placa      { get; set; } = string.Empty;
    public string Tipo       { get; set; } = string.Empty; // "Caminhão", "Van", "Moto", "Carro"
    public string Modelo     { get; set; } = string.Empty;
    public decimal Capacidade { get; set; }                // Capacidade em kg ou m³
    public bool   IsAtivo    { get; set; } = true;

    public ICollection<Entrega> Entregas { get; set; } = new List<Entrega>();
}
