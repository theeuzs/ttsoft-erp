using System;
using System.Collections.Generic;

namespace ERP.WPF.State;

// Classe estática: funciona como um "crachá" global que qualquer tela pode ler a qualquer momento
public static class AppSession
{
    public static Guid   UserId   { get; private set; }
    public static string UserName { get; private set; } = string.Empty;
    public static string RoleName { get; private set; } = string.Empty;

    // Flags de compatibilidade (ainda usadas em alguns lugares do sistema)
    public static bool IsGerente    { get; private set; }
    public static bool IsVendedor   { get; private set; }
    public static bool IsSupervisor { get; private set; }

    public static DateTime DataVencimentoLicenca { get; set; }
    public static IReadOnlyList<string> PermissionCodes { get; private set; } = Array.Empty<string>();

    // Limites operacionais vindos do banco — não mais hardcoded
    public static decimal MaxDiscountPercentage { get; private set; }
    public static decimal MaxSangriaValue       { get; private set; }

    // Caixa ativo do operador
    public static Guid? CaixaId { get; set; }

    // S10 FIX: JWT da API — obtido após login local, usado pelo ChatService para
    // autenticar no ERPChatHub via POST /api/auth/chat-token.
    // Vazio quando API indisponível — chat fica offline, resto do sistema funciona.
    public static string JwtToken   { get; set; } = string.Empty;

    // URL base da API — setada em App.xaml.cs, lida pelo LoginViewModel
    public static string ApiBaseUrl { get; set; } = string.Empty;

    public static void Login(
        Guid id,
        string name,
        string roleName,
        IEnumerable<string> permissionCodes,
        decimal maxDiscountPercentage,
        decimal maxSangriaValue = 0m)
    {
        UserId                = id;
        UserName              = name;
        RoleName              = roleName ?? string.Empty;
        PermissionCodes       = new List<string>(permissionCodes ?? Array.Empty<string>());
        MaxDiscountPercentage = maxDiscountPercentage;
        MaxSangriaValue       = maxSangriaValue;

        IsGerente    = string.Equals(RoleName, "Administrador", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(RoleName, "Gerente",       StringComparison.OrdinalIgnoreCase);
        IsVendedor   = string.Equals(RoleName, "Vendedor",      StringComparison.OrdinalIgnoreCase);
        IsSupervisor = string.Equals(RoleName, "Supervisor",    StringComparison.OrdinalIgnoreCase);

        ERP.Domain.CurrentUser.Id   = id;
        ERP.Domain.CurrentUser.Name = name;
    }

    public static void Logout()
    {
        UserId                = Guid.Empty;
        UserName              = string.Empty;
        RoleName              = string.Empty;
        IsGerente             = false;
        IsVendedor            = false;
        IsSupervisor          = false;
        PermissionCodes       = Array.Empty<string>();
        MaxDiscountPercentage = 0m;
        MaxSangriaValue       = 0m;
        CaixaId               = null;
        JwtToken              = string.Empty;

        ERP.Domain.CurrentUser.Id   = null;
        ERP.Domain.CurrentUser.Name = string.Empty;
    }
}