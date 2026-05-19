using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }

    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? MachineName { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
