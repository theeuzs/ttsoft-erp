namespace ERP.Application.Interfaces;

public interface IRequestTenant
{
    Guid    TenantId              { get; set; }
    Guid    UserId                { get; set; }
    string  UserName              { get; set; }
    // S9: limite de desconto da role — populado via claim JWT (API) ou AppSession (WPF).
    // Usado pelo SaleService para rejeitar DiscountPercent acima do teto da role.
    decimal MaxDiscountPercentage { get; set; }
}