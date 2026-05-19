using ERP.Domain.Enums;
using System;
using System.Collections.Generic;

namespace ERP.Application.DTOs;

// ── Product ──────────────────────────────────────────────
public record ProductDto(
    Guid Id, 
    string Name, 
    string? Barcode, 
    string? SKU,
    string? CategoryName, 
    string? Brand, 
    string Unit,
    decimal SalePrice, 
    decimal Stock, 
    decimal MinStock,           // ✅ Moved before optional parameters
    bool IsActive, 
    bool EmCampanha = false, 
    string? ImageUrl = null, 
    string? DescricaoDetalhada = null)
{
    // 👇 ADICIONANDO OS CAMPOS QUE FALTAVAM PARA A TELA DE PRODUTOS 👇
    public Guid? CategoryId { get; set; }
    public decimal QuantidadeGrade { get; set; } = 1;
    public Guid? BrandId { get; set; }
    public Guid? SupplierId { get; set; }
    public decimal OriginalCost { get; set; }
    public decimal IpiPercent { get; set; }
    public decimal IcmsPercent { get; set; }
    public decimal DesiredMarginPercent { get; set; }
    public decimal IdealStock { get; set; }
    public string? WarehouseLocation { get; set; }
    public bool AllowDiscount { get; set; }
    public bool AllowNegativeStock { get; set; }
    public string? NCM { get; set; }
    public string? CEST { get; set; }
    public string? CFOPPadrao { get; set; }
    public string? CSOSN { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal? WholesalePrice { get; set; }

    // Conversão de unidade
    public string?  UnidadeEstoque    { get; set; }
    public string?  UnidadeVenda      { get; set; }
    public decimal  FatorConversao    { get; set; } = 1m;
    public string?  LabelUnidadeVenda { get; set; }
    public bool     UsaConversaoUnidade => FatorConversao != 1m && !string.IsNullOrWhiteSpace(UnidadeVenda);

    // Produto Composto
    public Guid?   ParentProductId   { get; set; }
    public decimal ConversionFactor  { get; set; } = 1m;
    public string? ParentProductName { get; set; }

    // Sprint CDE: preços por grupo
    public decimal PrecoBRevendedor { get; set; } = 0;
    public decimal PrecoCAtacadista { get; set; } = 0;

    // Rastreamento de preço
    public DateTime? SalePriceChangedAt  { get; set; }
    public string?   SalePriceChangedBy  { get; set; }
    public DateTime? CostPriceChangedAt  { get; set; }
    public string?   CostPriceChangedBy  { get; set; }
}
    

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? SKU { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? SupplierId { get; set; }
    public string Unit { get; set; } = "UN";
    public bool IsActive { get; set; } = true;
    public bool AllowDiscount { get; set; } = true;
    public decimal OriginalCost { get; set; }
    public decimal IpiPercent { get; set; }
    public decimal IcmsPercent { get; set; }
    public decimal DesiredMarginPercent { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public decimal IdealStock { get; set; }
    public bool AllowNegativeStock { get; set; }
    public bool    EmCampanha         { get; set; } = false;
    public string? ImageUrl           { get; set; }
    public string? DescricaoDetalhada { get; set; }
    public string? WarehouseLocation { get; set; }
    public string? NCM { get; set; }
    public string? CEST { get; set; }
    public int MercadoriaOrigem { get; set; }
    public string? CFOPPadrao { get; set; }
    public string? CSOSN { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal? WholesalePrice { get; set; }

    // Conversão de unidade
    public string?  UnidadeEstoque    { get; set; }
    public string?  UnidadeVenda      { get; set; }
    public decimal  FatorConversao    { get; set; } = 1m;
    public string?  LabelUnidadeVenda { get; set; }
    public bool     UsaConversaoUnidade => FatorConversao != 1m && !string.IsNullOrWhiteSpace(UnidadeVenda);

    // Produto Composto
    public Guid?   ParentProductId   { get; set; }
    public decimal ConversionFactor  { get; set; } = 1m;

    // Rastreamento de preço
    public DateTime? SalePriceChangedAt  { get; set; }
    public string?   SalePriceChangedBy  { get; set; }
    public DateTime? CostPriceChangedAt  { get; set; }
    public string?   CostPriceChangedBy  { get; set; }
    // Sprint CDE
    public decimal PrecoBRevendedor { get; set; } = 0;
    public decimal PrecoCAtacadista { get; set; } = 0;
}

public class UpdateProductDto : CreateProductDto
{
    public Guid Id { get; set; }
}

// ── Customer ──────────────────────────────────────────────
public class CreateCustomerDto
{
    public string  Document          { get; set; } = string.Empty;
    public string  Name              { get; set; } = string.Empty;
    public string? StateRegistration { get; set; }
    public string? Phone             { get; set; }
    public string? Email             { get; set; }
    public string? ZipCode           { get; set; }
    public string? Street            { get; set; }
    public string? Number            { get; set; }
    public string? Complement        { get; set; }
    public string? Neighborhood      { get; set; }
    public string? City              { get; set; }
    public string? State             { get; set; }
    // Sprint CDE
    public int     GrupoPreco        { get; set; } = 0;
    public decimal LimiteCredito     { get; set; } = 0;
}

public record CustomerDto(
    Guid    Id,
    string  Document,
    string  Name,
    string? Phone,
    string? City,
    decimal HaverBalance,
    string? Street        = null,
    string? Number        = null,
    string? Neighborhood  = null,
    string? State         = null,
    string? ZipCode       = null,
    string? Ie            = null,
    string? Email         = null,
    int     GrupoPreco    = 0,
    decimal LimiteCredito = 0,
    decimal SaldoDevedor  = 0
);

// ── Sale ──────────────────────────────────────────────────

// NOVO: DTO para receber cada pagamento individual da tela
public class CreateSalePaymentDto
{
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
}

public class CreateSaleDto
{
    public Guid? CustomerId { get; set; }
    public string? SellerName { get; set; }
    
    // 👇 ADICIONADO: O ID de quem está operando o caixa
    public Guid UsuarioId { get; set; } 
    public string? Notes { get; set; }
    
    public decimal DiscountAmount { get; set; }
    
    // NOVO: Lista de pagamentos substituindo o pagamento único
    public List<CreateSalePaymentDto> Payments { get; set; } = new();
    
    public List<CreateSaleItemDto> Items { get; set; } = new();
}

public class CreateSaleItemDto
{
    public Guid    ProductId       { get; set; }
    public decimal Quantity        { get; set; }
    public decimal UnitPrice       { get; set; }
    public decimal DiscountPercent { get; set; }
    /// <summary>Fator de conversão (ex: 6 para cano em barras). Default 1.</summary>
    public decimal FatorConversao  { get; set; } = 1m;
    /// <summary>Quantidade real a baixar do estoque = Quantity * FatorConversao</summary>
    public decimal QuantidadeEstoque => Quantity * FatorConversao;
    /// <summary>Total exato do carrinho. Evita diferença por arredondamento do UnitPrice.</summary>
    public decimal TotalItem { get; set; }
}

// NOVO: DTO para ler os pagamentos no histórico
public record SalePaymentDto(string PaymentMethod, decimal Amount);

public record SaleDto(
    Guid Id, 
    string SaleNumber, 
    string? CustomerName,
    string? SellerName, // 👈 ADICIONE ESTA LINHA AQUI!
    DateTime SaleDate, 
    SaleStatus Status, 
    string PaymentMethods, 
    decimal Total,
    string? NfceChave = null,
    string? NfceNumero = null,
    string? NfceUrlDanfe = null,
    string? NfceAmbiente = null,
    string? NfceStatusFocus = null,
    string? NfceReferencia = null
);
    

public record SaleDetailDto(
    Guid Id, 
    string SaleNumber, 
    string? CustomerName,
    string? SellerName,
    Guid? CustomerId, 
    string? CustomerPhone, // 👈 A MÁGICA NOVA AQUI
    DateTime SaleDate, 
    SaleStatus Status, 
    decimal Subtotal, 
    decimal DiscountAmount, 
    decimal Total,
    List<SalePaymentDto> Payments, 
    List<SaleItemDto> Items,
    string? Observation);

public record SaleItemDto(Guid ProductId, string ProductName, decimal Quantity, decimal UnitPrice, decimal DiscountPercent, decimal TotalPrice)
{
    public string?  LabelUnidadeVenda { get; init; }
    public string?  UnidadeEstoque    { get; init; }
    public decimal  FatorConversao    { get; init; } = 1m;
}

// ── Dashboard ──────────────────────────────────────────────
public record DashboardDto(
    decimal TodaySales, decimal MonthSales,
    decimal AverageTicket, int TotalOrders,
    List<TopProductDto> TopProducts,
    // Campos extras para o Dashboard completo
    decimal ExpensesThisMonth,
    int     LowStockCount,
    int     ContasVencendoHoje,
    decimal ValorVencendoHoje,
    List<SaleDto> RecentSales,
    List<ProductDto> LowStockProducts,
    List<(string Label, double Valor)> VendasSemana);

public record TopProductDto(string Name, decimal QuantitySold);

// ── Auth & Users ──────────────────────────────────────────────
public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty; 
    public List<string> Permissions { get; set; } = new();
    
    // Limite máximo de desconto percentual permitido para o perfil atual
    public decimal MaxDiscountPercentage { get; set; }

    // Limite máximo de sangria permitido para o perfil atual
    public decimal MaxSangriaValue { get; set; }

    public UserDto() { }
}
public class CreateUserDto
{
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    // Agora passamos o ID da Role (Administrador, Vendedor, etc)
    public Guid RoleId { get; set; } 
}

// ==========================================
// DTOs (As Classes de Dados)
// ==========================================

// O Caixa em si (Sessão do dia)
public class CaixaDto
{
    public Guid Id { get; set; }
    public int NumeroCaixa { get; set; } // Aquele "#8" que aparece no seu print
    public string OperadorNome { get; set; } = string.Empty; // Ex: "Matheus"
    public DateTime DataAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public decimal ValorAbertura { get; set; }
    public StatusCaixa Status { get; set; }
    
    // Lista de tudo que aconteceu nesse caixa
    public List<CaixaMovimentoDto> Movimentos { get; set; } = new();
}

// Cada dinheiro que entra ou sai
public class CaixaMovimentoDto
{
    public Guid Id { get; set; }
    public Guid CaixaId { get; set; }
    public DateTime DataHora { get; set; }
    public TipoMovimentoCaixa Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty; // Ex: "ABERTURA", "VENDA #123", "PAGAMENTO MOTOBOY"
    public decimal Valor { get; set; } // Positivo para entrada, Negativo para saída
    public ERP.Domain.Enums.PaymentMethod? FormaPagamento { get; set; }
}

// DTO para a gente enviar do C# para o banco na hora de abrir a tela
public class AbrirCaixaDto
{
    public Guid UsuarioId { get; set; }
    public string? OperadorNome { get; set; }   // 🟢 NOVO
    public decimal ValorAbertura { get; set; }
}

// ── Supplier / Category / Brand ───────────────────────────────────────────
public record SupplierDto(Guid Id, string Name);
public record CategoryDto(Guid Id, string Name);
public record BrandDto(Guid Id, string Name);

// ── AuditLog ──────────────────────────────────────────────────────────────
public class AuditLogDto
{
    public Guid     Id         { get; init; }
    public string?  UserName   { get; init; }
    public string?  Action     { get; init; }
    public string?  EntityType { get; init; }
    public string?  EntityId   { get; init; }
    public DateTime Timestamp  { get; init; }
    public string?  MachineName{ get; init; }
    public string?  OldValues  { get; init; }
    public string?  NewValues  { get; init; }

    // Campos ignorados na comparação (técnicos / sem valor para o usuário)
    private static readonly HashSet<string> _camposIgnorados = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "TenantId", "CreatedAt", "UpdatedAt", "IsDeleted",
        "SaleId", "ProductId", "CustomerId", "SellerId"
    };

    /// <summary>
    /// Compara OldValues e NewValues e retorna apenas os campos que realmente mudaram,
    /// em formato legível. Ex: "Preco: 10,00 ➔ 15,00 | Estoque: 50 ➔ 45"
    /// Para INSERT mostra os campos principais. Para DELETE mostra resumo.
    /// </summary>
    public string ResumoAlteracoes
    {
        get
        {
            try
            {
                if (Action == "INSERT" && !string.IsNullOrWhiteSpace(NewValues))
                    return FormatarInsert(NewValues);

                if (Action == "DELETE" && !string.IsNullOrWhiteSpace(OldValues))
                    return FormatarDelete(OldValues);

                if (Action == "UPDATE" && !string.IsNullOrWhiteSpace(OldValues) && !string.IsNullOrWhiteSpace(NewValues))
                    return CompararAlteracoes(OldValues, NewValues);

                return string.Empty;
            }
            catch { return "Erro ao processar alterações"; }
        }
    }

    private static string FormatarInsert(string newJson)
    {
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(newJson);
        if (dict == null) return string.Empty;

        // Prioriza campos mais relevantes para exibir no INSERT
        var prioridade = new[] { "Name", "ProductName", "Descricao", "SaleNumber", "Username", "Total", "Status" };
        var partes = new List<string>();

        foreach (var campo in prioridade)
        {
            if (dict.TryGetValue(campo, out var val) && val.ValueKind != System.Text.Json.JsonValueKind.Null)
                partes.Add($"{campo}: {FormatarValor(val)}");
        }

        return partes.Any() ? string.Join(" | ", partes) : "Novo registro criado";
    }

    private static string FormatarDelete(string oldJson)
    {
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(oldJson);
        if (dict == null) return "Registro excluído";

        var prioridade = new[] { "Name", "ProductName", "Descricao", "SaleNumber", "Username" };
        foreach (var campo in prioridade)
            if (dict.TryGetValue(campo, out var val) && val.ValueKind != System.Text.Json.JsonValueKind.Null)
                return $"Excluído: {FormatarValor(val)}";

        return "Registro excluído";
    }

    private static string CompararAlteracoes(string oldJson, string newJson)
    {
        var antes  = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(oldJson);
        var depois = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(newJson);

        if (antes == null || depois == null) return string.Empty;

        var alteracoes = new List<string>();

        // Como o AppDbContext já salva APENAS os campos modificados nos JSONs de UPDATE,
        // todos os campos presentes são alterações reais — basta exibi-los.
        foreach (var kvp in depois)
        {
            if (_camposIgnorados.Contains(kvp.Key)) continue;

            string strDepois = FormatarValor(kvp.Value);
            string strAntes  = antes.TryGetValue(kvp.Key, out var va) ? FormatarValor(va) : "(novo)";

            // Normaliza para comparação: remove zeros à direita em decimais
            string normAntes  = NormalizarNumero(strAntes);
            string normDepois = NormalizarNumero(strDepois);

            if (normAntes != normDepois)
                alteracoes.Add($"{kvp.Key}: {strAntes} ➔ {strDepois}");
        }

        return alteracoes.Any() ? string.Join("  |  ", alteracoes) : "Sem alterações detectadas";
    }

    /// <summary>Normaliza "5.90" e "5.9" para o mesmo valor para evitar falsos positivos.</summary>
    private static string NormalizarNumero(string val)
    {
        if (decimal.TryParse(val, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal d))
            return d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return val;
    }

    private static string FormatarValor(System.Text.Json.JsonElement el) => el.ValueKind switch
    {
        System.Text.Json.JsonValueKind.Null   => "(vazio)",
        System.Text.Json.JsonValueKind.String => el.GetString() ?? "(vazio)",
        System.Text.Json.JsonValueKind.Number => el.GetRawText(),
        System.Text.Json.JsonValueKind.True   => "Sim",
        System.Text.Json.JsonValueKind.False  => "Não",
        _                                     => el.GetRawText()
    };

    // Construtor para compatibilidade com o código existente
    public AuditLogDto() { }
}

// ── Devolução Parcial ─────────────────────────────────────────────────────
public class DevolucaoItemDto
{
    public Guid    ProductId   { get; set; }
    public string  ProductName { get; set; } = string.Empty;
    public decimal QuantidadeVendida  { get; set; }
    public decimal QuantidadeDevolver { get; set; }
    public decimal UnitPrice   { get; set; }
    public decimal ValorTotal  => QuantidadeDevolver * UnitPrice;
}

public class CreateDevolucaoDto
{
    public Guid              VendaId      { get; set; }
    public Guid?             CustomerId   { get; set; }
    public string            OperadorNome { get; set; } = string.Empty;
    public string            Motivo       { get; set; } = string.Empty;
    public List<DevolucaoItemDto> Itens   { get; set; } = new();
}

public record DevolucaoResultDto(
    decimal ValorTotalDevolvido,
    string  NumeroVendaOriginal,
    string  NomeCliente,
    List<DevolucaoItemDto> ItensDevolvidos);
// ── DTOs de Cargos e Permissões ────────────────────────────────────────────
public class RoleDto
{
    public Guid    Id                    { get; set; }
    public string  Name                  { get; set; } = string.Empty;
    public decimal MaxDiscountPercentage { get; set; }
    public decimal MaxSangriaValue       { get; set; }
    public decimal PercentualComissao    { get; set; } = 0; // Sprint CDE
    public List<string> PermissionCodes  { get; set; } = new();
    public List<Guid>   PermissionIds    { get; set; } = new();
    public int  UserCount                { get; set; }
    public bool IsProtected              { get; set; }
}

public class PermissionDto
{
    public Guid   Id          { get; set; }
    public string Code        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Group       { get; set; } = string.Empty;
}

public class UpdateRoleDto
{
    public Guid    Id                    { get; set; }
    public decimal MaxDiscountPercentage { get; set; }
    public decimal MaxSangriaValue       { get; set; }
    public decimal PercentualComissao    { get; set; } = 0; // Sprint CDE
    public List<Guid> PermissionIds      { get; set; } = new();
}

public class CreateRoleDto
{
    public string  Name                  { get; set; } = string.Empty;
    public decimal MaxDiscountPercentage { get; set; }
    public decimal MaxSangriaValue       { get; set; }
    public List<Guid> PermissionIds      { get; set; } = new();
}

// ── DTO de resultado de login (com suporte a mensagens de bloqueio) ─────────
public class LoginResultDto
{
    public bool    Sucedeu  { get; private set; }
    public string? Mensagem { get; private set; }
    public UserDto? Usuario  { get; private set; }

    public static LoginResultDto Sucesso(UserDto usuario) =>
        new() { Sucedeu = true, Usuario = usuario };

    public static LoginResultDto Falhou(string mensagem) =>
        new() { Sucedeu = false, Mensagem = mensagem };
}

// ── DTOs de Parcelamento ────────────────────────────────────────────────────
public class GerarParcelasDto
{
    public Guid       SaleId          { get; set; }
    public Guid       CustomerId      { get; set; }
    public decimal    ValorTotal      { get; set; }
    public int        NumeroParcelas  { get; set; } = 1;
    public DateTime   PrimeiroVencimento { get; set; }
    /// <summary>Intervalo em dias entre parcelas. Padrão: 30 dias.</summary>
    public int        IntervalosDias  { get; set; } = 30;
    public string     FormaPagamento  { get; set; } = "A Prazo";
    public string     Descricao       { get; set; } = string.Empty;
}

public class ParcelaDto
{
    public Guid     Id              { get; set; }
    public int      NumeroParcela   { get; set; }
    public int      TotalParcelas   { get; set; }
    public decimal  ValorTotal      { get; set; }
    public decimal  ValorRecebido   { get; set; }
    public decimal  ValorRestante   => ValorTotal - ValorRecebido;
    public DateTime DataVencimento  { get; set; }
    public DateTime? DataPagamento  { get; set; }
    public string   Status          { get; set; } = string.Empty;
    public string   FormaPagamento  { get; set; } = string.Empty;
    public Guid?    ParcelamentoId  { get; set; }
}
