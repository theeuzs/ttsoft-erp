using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>Achado de auditoria (06/08/2026): essa entidade não tinha
/// TenantId nenhum (nem herdado) — dependia inteiramente de filtro manual
/// em cada consumidor, sem rede de proteção nenhuma. Passou a herdar
/// BaseEntity, igual toda outra entidade "de fila"/rascunho do sistema.</summary>
public class NfePendente : BaseEntity
{
    public Guid VendaId { get; set; }
    public string TipoNota { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public DateTime DataFalha { get; set; }
    public int Tentativas { get; set; }
    public string? UltimaMensagemErro { get; set; }
}