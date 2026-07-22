// ── ERP.Domain/Entities/ProcessingSession.cs ───────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Uma rodada de sincronização (uma execução do motor de processamento, seja
/// disparada manualmente ou por um agendador futuro). Nome genérico de propósito
/// — não é "MarketplaceSync" porque no futuro pode cobrir mais de um canal na
/// mesma rodada.
/// </summary>
public class ProcessingSession : BaseEntity
{
    /// <summary>Null quando a rodada cobre todos os canais ativos do tenant.</summary>
    public Guid?          SalesChannelId { get; set; }
    public SalesChannel?  SalesChannel   { get; set; }

    public DateTime  IniciadoEm   { get; set; } = DateTime.UtcNow;
    public DateTime? FinalizadoEm { get; set; }

    public ProcessingSessionStatus Status { get; set; } = ProcessingSessionStatus.EmAndamento;

    public int TotalPedidosProcessados { get; set; }
    public int TotalConflitos          { get; set; }
    public int TotalErros              { get; set; }
}
