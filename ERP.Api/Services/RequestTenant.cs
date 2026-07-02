using ERP.Application.Interfaces;

namespace ERP.Api.Services;

public class RequestTenant : IRequestTenant
{
    public Guid    TenantId              { get; set; } = Guid.Empty;
    public Guid    UserId                { get; set; } = Guid.Empty;
    public string  UserName              { get; set; } = string.Empty;
    public decimal MaxDiscountPercentage { get; set; } = 0m;
    // S13: limite de sangria — mesmo padrão do MaxDiscountPercentage (S9)
    public decimal MaxSangriaValue       { get; set; } = 0m;
}