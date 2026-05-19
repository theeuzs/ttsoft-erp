using System;
using System.Linq;

namespace ERP.WPF.State;

/// <summary>
/// Centraliza a verificação de permissões da sessão atual.
/// Todos os limites operacionais vêm do banco via AppSession — sem hardcode.
/// </summary>
public static class PermissionChecker
{
    // ── Códigos de permissão do sistema ────────────────────────────────────
    // PDV / Vendas
    public const string SaleDiscount     = "sale.discount";
    public const string SaleCancel       = "sale.cancel";
    public const string SaleReturn       = "sale.return";

    // Caixa
    public const string CashSangria      = "cash.sangria";
    public const string CashViewSummary  = "cash.view.summary";

    // Produtos / Estoque
    public const string ProductEdit      = "product.edit";
    public const string ProductEditPrice = "product.edit.price";
    public const string StockAdjust      = "stock.adjust";

    // Clientes / Haver
    public const string HaverEdit        = "haver.edit";

    // Financeiro
    public const string ReportFinancial  = "report.financial";
    public const string FinanceiroView   = "financeiro.view";
    public const string DespesasView     = "despesas.view";
    public const string FluxoCaixaView   = "fluxocaixa.view";
    public const string MargemView       = "margem.view";

    // Relatórios / Operacionais
    public const string AuditView        = "audit.view";
    public const string ComprasView      = "compras.view";
    public const string InventarioView   = "inventario.view";
    public const string NotasFiscais     = "notasfiscais.view";

    // Administração
    public const string UsersView        = "users.view";
    public const string ConfigView       = "config.view";

    /// <summary>Verifica se o usuário logado possui o código de permissão informado.</summary>
    public static bool Has(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return false;

        var codes = AppSession.PermissionCodes;
        if (codes == null || codes.Count == 0) return false;

        return codes.Any(c => string.Equals(c, permissionCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Limite máximo de desconto do cargo atual — vem do banco, sem hardcode.
    /// </summary>
    public static decimal GetMaxDiscountPercentage() => AppSession.MaxDiscountPercentage;

    /// <summary>
    /// Limite máximo de sangria do cargo atual — vem do banco, sem hardcode.
    /// </summary>
    public static decimal GetMaxSangriaValue() => AppSession.MaxSangriaValue;
}
