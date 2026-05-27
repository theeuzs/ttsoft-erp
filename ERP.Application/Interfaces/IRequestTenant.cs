namespace ERP.Application.Interfaces;

public interface IRequestTenant
{
    Guid   TenantId { get; set; }
    Guid   UserId   { get; set; }
    string UserName { get; set; }
}
