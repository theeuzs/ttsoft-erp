using Microsoft.AspNetCore.Authorization;

namespace ERP.Api.Security;

/// <summary>
/// Restringe acesso ao controller/action a usuários com o código de permissão informado.
/// Uso: [HasPermission(Permissions.SaleCancel)]
/// Equivalente a [Authorize(Policy = "sale.cancel")] mas com IntelliSense e sem magic strings.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) : base(permission)
    {
    }
}
