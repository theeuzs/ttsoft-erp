using ERP.Domain.Interfaces;
using ERP.WPF.State;

namespace ERP.WPF.Services;

/// <summary>
/// Implementação de ICurrentUser para WPF.
/// Delega ao AppSession (que continua sendo a fonte da verdade para a sessão ativa),
/// mas expõe a interface injetável que permite mock em testes unitários.
/// </summary>
public class CurrentUserService : ICurrentUser
{
    public Guid   UserId   => AppSession.UserId;
    public string UserName => AppSession.UserName;
    public string RoleName => AppSession.RoleName;

    public decimal MaxDiscountPercentage => AppSession.MaxDiscountPercentage;
    public decimal MaxSangriaValue       => AppSession.MaxSangriaValue;

    public bool IsAuthenticated => AppSession.UserId != Guid.Empty;

    public IReadOnlyList<string> PermissionCodes => AppSession.PermissionCodes;

    public bool Has(string permissionCode)
        => PermissionChecker.Has(permissionCode);
}
