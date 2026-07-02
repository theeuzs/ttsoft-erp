namespace ERP.Application.Interfaces;

public interface IRequestTenant
{
    Guid    TenantId              { get; set; }
    Guid    UserId                { get; set; }
    string  UserName              { get; set; }
    // S9: limite de desconto da role — populado via claim JWT (API) ou AppSession (WPF).
    decimal MaxDiscountPercentage { get; set; }
    // S13: limite de sangria da role — mesmo padrão do MaxDiscountPercentage.
    decimal MaxSangriaValue       { get; set; }
}