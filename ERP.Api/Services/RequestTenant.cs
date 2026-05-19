using ERP.Application.Interfaces;

namespace ERP.Api.Services;

public class RequestTenant : IRequestTenant
{
    public Guid   TenantId { get; set; } = Guid.Empty;
    public Guid   UserId   { get; set; } = Guid.Empty;
    public string UserName { get; set; } = string.Empty;
}