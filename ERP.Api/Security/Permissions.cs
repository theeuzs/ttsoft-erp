namespace ERP.Api.Security;

/// <summary>
/// Códigos de permissão do sistema — espelho dos constantes do PermissionChecker no WPF.
/// Cada código corresponde a uma policy ASP.NET Core registrada em Program.cs.
/// Uso: [HasPermission(Permissions.SaleCancel)]
/// </summary>
public static class Permissions
{
    // ── PDV / Vendas ──────────────────────────────────────────────────────────
    public const string SaleDiscount    = "sale.discount";
    public const string SaleCancel      = "sale.cancel";
    public const string SaleReturn      = "sale.return";

    // ── Caixa ─────────────────────────────────────────────────────────────────
    public const string CashSangria     = "cash.sangria";
    public const string CashViewSummary = "cash.view.summary";

    // ── Produtos / Estoque ────────────────────────────────────────────────────
    public const string ProductEdit      = "product.edit";
    public const string ProductEditPrice = "product.edit.price";
    public const string StockAdjust      = "stock.adjust";

    // ── Clientes / Haver ──────────────────────────────────────────────────────
    public const string HaverEdit       = "haver.edit";

    // ── Financeiro ────────────────────────────────────────────────────────────
    public const string ReportFinancial = "report.financial";
    public const string FinanceiroView  = "financeiro.view";
    public const string DespesasView    = "despesas.view";
    public const string FluxoCaixaView  = "fluxocaixa.view";
    public const string MargemView      = "margem.view";

    // ── Relatórios / Operacionais ─────────────────────────────────────────────
    public const string AuditView       = "audit.view";
    public const string ComprasView     = "compras.view";
    public const string InventarioView  = "inventario.view";
    public const string NotasFiscaisView = "notasfiscais.view";

    // ── Administração ─────────────────────────────────────────────────────────
    public const string UsersView   = "users.view";
    public const string ConfigView  = "config.view";
    /// <summary>
    /// Permite criar, editar e excluir cargos e suas permissões.
    /// Separado de users.view para evitar escalada de privilégio:
    /// visualizar ≠ editar o que cada perfil pode fazer.
    /// </summary>
    public const string RoleManage  = "role.manage";

    /// <summary>Todas as permissões do sistema — usado para seed de Administrador e testes.</summary>
    public static readonly string[] All =
    [
        SaleDiscount, SaleCancel, SaleReturn,
        CashSangria, CashViewSummary,
        ProductEdit, ProductEditPrice, StockAdjust,
        HaverEdit,
        ReportFinancial, FinanceiroView, DespesasView, FluxoCaixaView, MargemView,
        AuditView, ComprasView, InventarioView, NotasFiscaisView,
        UsersView, ConfigView, RoleManage
    ];
}