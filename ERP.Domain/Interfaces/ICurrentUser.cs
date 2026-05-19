namespace ERP.Domain.Interfaces;

/// <summary>
/// Contrato para acesso ao usuário autenticado atual.
/// Injetável via DI — substitui a classe estática AppSession para código testável.
/// </summary>
public interface ICurrentUser
{
    Guid   UserId   { get; }
    string UserName { get; }
    string RoleName { get; }

    decimal MaxDiscountPercentage { get; }
    decimal MaxSangriaValue       { get; }

    bool IsAuthenticated { get; }

    /// <summary>Verifica se o usuário possui o código de permissão informado.</summary>
    bool Has(string permissionCode);

    /// <summary>Retorna todos os códigos de permissão do usuário atual.</summary>
    IReadOnlyList<string> PermissionCodes { get; }
}
