// ── ERP.Domain/Entities/ChatMessage.cs ───────────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Mensagem persistida do chat interno.
/// Herda de BaseEntity: Id (Guid), TenantId, CreatedAt, UpdatedAt, IsDeleted.
/// TenantId garante isolamento multi-loja sem HasQueryFilter adicional.
/// </summary>
public class ChatMessage : BaseEntity
{
    /// <summary>Nome de exibição do remetente (vem do Context.Items no hub).</summary>
    public string RemetenteNome { get; set; } = string.Empty;

    /// <summary>Texto da mensagem. Máximo 2000 chars — validado no hub antes de salvar.</summary>
    public string Mensagem { get; set; } = string.Empty;

    /// <summary>
    /// Sala opcional — null = broadcast geral do tenant,
    /// valor = isolamento por filial (ex: "filial-1").
    /// </summary>
    public string? Sala { get; set; }

    /// <summary>
    /// Flag de leitura global da mensagem.
    /// true quando ao menos um receptor além do remetente conectou-se após o envio.
    /// Para rastreamento por destinatário, implementar ChatMessageRead (Sprint 4).
    /// </summary>
    public bool IsRead { get; set; } = false;
}
