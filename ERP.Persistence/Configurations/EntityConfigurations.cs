using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Persistence.Configurations;

// ─── NOTA: Os HasQueryFilter globais (soft-delete + TenantId) estão centralizados
// no AppDbContext.OnModelCreating para evitar conflito de filtros duplicados.
// Aqui ficam apenas as configurações de schema (colunas, índices, relações). ───

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Barcode).HasMaxLength(50);
        b.Property(p => p.SKU).HasMaxLength(50);
        b.Property(p => p.Unit).HasMaxLength(10).HasDefaultValue("UN");
        b.Property(p => p.SalePrice).HasColumnType("decimal(18,4)");
        b.Property(p => p.OriginalCost).HasColumnType("decimal(18,4)");
        b.Property(p => p.CostPrice).HasColumnType("decimal(18,4)");
        b.Property(p => p.IpiPercent).HasColumnType("decimal(5,2)");
        b.Property(p => p.IcmsPercent).HasColumnType("decimal(5,2)");
        b.Property(p => p.DesiredMarginPercent).HasColumnType("decimal(5,2)");
        b.Property(p => p.Stock).HasColumnType("decimal(18,4)");
        b.Property(p => p.MinStock).HasColumnType("decimal(18,4)");
        b.Property(p => p.IdealStock).HasColumnType("decimal(18,4)");
        b.Property(p => p.WholesaleMinQuantity).HasColumnType("decimal(18,4)");
        b.Property(p => p.WholesalePrice).HasColumnType("decimal(18,4)");
        b.Property(p => p.WarehouseLocation).HasMaxLength(100);
        b.Property(p => p.NCM).HasMaxLength(10);
        b.Property(p => p.CEST).HasMaxLength(10);
        b.Property(p => p.CFOPPadrao).HasMaxLength(10);
        b.Property(p => p.CSOSN).HasMaxLength(10);
        b.HasOne(p => p.Category).WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
        b.Property(p => p.ConversionFactor).HasColumnType("decimal(18,6)").HasDefaultValue(1m);
        b.Ignore(p => p.FinalCost)
            .Ignore(p => p.RealMarginPercent)
            .Ignore(p => p.UnitProfit)
            .Ignore(p => p.Markup)
            .Ignore(p => p.IsProdutoFilho);

        // Auto-relacionamento: Produto Filho → Produto Pai
        // DeleteBehavior.Restrict: impede exclusão de produto pai que tenha filhos
        b.HasOne(p => p.ParentProduct)
            .WithMany()
            .HasForeignKey(p => p.ParentProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // ── Índices para busca rápida de produto ──────────────────────────────
        // Cobre: listagem do catálogo, busca por nome, busca por código de barras
        b.HasIndex(p => new { p.TenantId, p.IsDeleted })
            .HasDatabaseName("IX_Products_TenantId_IsDeleted");

        b.HasIndex(p => new { p.TenantId, p.Name, p.IsDeleted })
            .HasDatabaseName("IX_Products_TenantId_Name_IsDeleted");

        b.HasIndex(p => new { p.TenantId, p.Barcode })
            .HasDatabaseName("IX_Products_TenantId_Barcode")
            .HasFilter("[Barcode] IS NOT NULL");
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.Document).HasMaxLength(18).IsRequired(false);
        b.Property(c => c.Phone).HasMaxLength(20);
        b.Property(c => c.Email).HasMaxLength(200);
        b.Property(c => c.StateRegistration).HasMaxLength(30);
        b.Property(c => c.ZipCode).HasMaxLength(10);
        b.Property(c => c.Street).HasMaxLength(200);
        b.Property(c => c.Number).HasMaxLength(20);
        b.Property(c => c.Complement).HasMaxLength(100);
        b.Property(c => c.Neighborhood).HasMaxLength(100);
        b.Property(c => c.City).HasMaxLength(100);
        b.Property(c => c.State).HasMaxLength(2);
        b.Property(c => c.HaverBalance).HasColumnType("decimal(18,2)");
        b.HasIndex(c => c.Document).IsUnique().HasFilter("[Document] IS NOT NULL");
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.SaleNumber).HasMaxLength(30).IsRequired();
        b.Property(s => s.Subtotal).HasColumnType("decimal(18,2)");
        b.Property(s => s.DiscountAmount).HasColumnType("decimal(18,2)");
        b.Property(s => s.Total).HasColumnType("decimal(18,2)");
        b.Property(s => s.SellerName).HasMaxLength(200);
        b.HasOne(s => s.Customer).WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(s => s.Items).WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);

        // ── Índices compostos para relatórios ─────────────────────────────
        b.HasIndex(s => new { s.TenantId, s.SaleDate, s.Status })
            .HasDatabaseName("IX_Sales_TenantId_SaleDate_Status");
        b.HasIndex(s => new { s.TenantId, s.SaleDate })
            .HasDatabaseName("IX_Sales_TenantId_SaleDate");
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        b.Property(i => i.Quantity).HasColumnType("decimal(18,4)");
        b.Property(i => i.UnitPrice).HasColumnType("decimal(18,4)");
        b.Property(i => i.DiscountPercent).HasColumnType("decimal(5,2)");
        b.Ignore(i => i.TotalPrice);
        b.HasOne(i => i.Product).WithMany(p => p.SaleItems)
            .HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

        // ABC faz GroupBy(ProductId) com JOIN em Sale — cobertura do SaleId
        b.HasIndex(i => new { i.SaleId, i.ProductId })
            .HasDatabaseName("IX_SaleItems_SaleId_ProductId");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FIX #1 — Warnings CS0618 "no HasColumnType" eliminados
//  Caixa.ValorAbertura, CaixaMovimento.Valor, SalePayment.Amount
// ═══════════════════════════════════════════════════════════════════════════════

public class CaixaConfiguration : IEntityTypeConfiguration<Caixa>
{
    public void Configure(EntityTypeBuilder<Caixa> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.OperadorNome).HasMaxLength(200).IsRequired();
        // ← CORREÇÃO: declarar o tipo da coluna decimal evita o warning CS0618
        b.Property(c => c.ValorAbertura).HasColumnType("decimal(18,2)");
        b.HasMany(c => c.Movimentos)
            .WithOne(m => m.Caixa)
            .HasForeignKey(m => m.CaixaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CaixaMovimentoConfiguration : IEntityTypeConfiguration<CaixaMovimento>
{
    public void Configure(EntityTypeBuilder<CaixaMovimento> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Descricao).HasMaxLength(500).IsRequired();
        // ← CORREÇÃO: sem isso o EF emitia warning sobre precision implícita
        b.Property(m => m.Valor).HasColumnType("decimal(18,2)");
    }
}

public class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> b)
    {
        b.HasKey(p => p.Id);
        // ← CORREÇÃO: Amount sem HasColumnType gerava warning
        // HasOne removido — relação já configurada em SaleConfiguration
        b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
    }
}

// ── Restante das configurações (inalteradas) ──────────────────────────────────

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.HasKey(r => r.Id);
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.Property(r => r.MaxDiscountPercentage).HasColumnType("decimal(5,2)");
        b.Property(r => r.MaxSangriaValue).HasColumnType("decimal(18,2)");
        b.HasMany(r => r.Users).WithOne(u => u.Role)
            .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(r => r.Permissions).WithMany(p => p.Roles)
            .UsingEntity("PermissionRole");
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Code).HasMaxLength(100).IsRequired();
        b.Property(p => p.Description).HasMaxLength(300);
        b.HasIndex(p => p.Code).IsUnique();
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Name).HasMaxLength(200).IsRequired();
        b.Property(u => u.Username).HasMaxLength(100).IsRequired();
        b.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        b.HasIndex(u => new { u.Username, u.TenantId }).IsUnique();
        b.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
        b.Property(u => u.LockoutEndUtc).IsRequired(false);
        b.HasOne(u => u.Role).WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasMany(x => x.Products).WithOne(p => p.Brand)
            .HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasMany(x => x.Products).WithOne(p => p.Supplier)
            .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MovimentoHaverConfiguration : IEntityTypeConfiguration<MovimentoHaver>
{
    public void Configure(EntityTypeBuilder<MovimentoHaver> b)
    {
        b.HasKey(m => m.Id);
        b.ToTable("MovimentosHaver");
        b.Property(m => m.Valor).HasColumnType("decimal(18,2)");
        b.Property(m => m.Tipo).HasMaxLength(20).IsRequired();
        b.Property(m => m.Descricao).HasMaxLength(500);
        b.Property(m => m.OperadorNome).HasMaxLength(200);
        b.HasOne(m => m.Customer).WithMany()
            .HasForeignKey(m => m.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContaReceberConfiguration : IEntityTypeConfiguration<ContaReceber>
{
    public void Configure(EntityTypeBuilder<ContaReceber> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.ValorTotal).HasColumnType("decimal(18,2)");
        b.Property(c => c.ValorRecebido).HasColumnType("decimal(18,2)");
        b.Property(c => c.Status).HasMaxLength(20).IsRequired();
        b.Property(c => c.Descricao).HasMaxLength(500);
        b.HasOne(c => c.Customer).WithMany()
            .HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);

        // FluxoCaixa filtra TenantId + DataVencimento + Status
        b.HasIndex(c => new { c.TenantId, c.DataVencimento, c.Status })
            .HasDatabaseName("IX_ContasReceber_TenantId_Vencimento_Status");
    }
}

public class ContaPagarConfiguration : IEntityTypeConfiguration<ContaPagar>
{
    public void Configure(EntityTypeBuilder<ContaPagar> b)
    {
        b.HasKey(c => c.Id);

        // DRE e FluxoCaixa filtram TenantId + DataVencimento + Status
        b.HasIndex(c => new { c.TenantId, c.DataVencimento, c.Status })
            .HasDatabaseName("IX_ContasPagar_TenantId_Vencimento_Status");
        b.Property(c => c.Valor).HasColumnType("decimal(18,2)");
        b.Property(c => c.Descricao).HasMaxLength(500).IsRequired();
        b.Property(c => c.Categoria).HasMaxLength(100);
        b.Property(c => c.Status).HasMaxLength(20).IsRequired();
    }
}

public class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> b)
    {
        b.HasKey(o => o.Id);
        b.Property(o => o.Numero).HasMaxLength(30).IsRequired();
        b.Property(o => o.CustomerName).HasMaxLength(200);
        b.Property(o => o.SellerName).HasMaxLength(200);
        b.Property(o => o.ValorTotal).HasColumnType("decimal(18,2)");
        b.HasMany(o => o.Itens).WithOne(i => i.Orcamento)
            .HasForeignKey(i => i.OrcamentoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrcamentoItemConfiguration : IEntityTypeConfiguration<OrcamentoItem>
{
    public void Configure(EntityTypeBuilder<OrcamentoItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        b.Property(i => i.Quantity).HasColumnType("decimal(18,4)");
        b.Property(i => i.UnitPrice).HasColumnType("decimal(18,4)");
        b.Property(i => i.DiscountPercent).HasColumnType("decimal(5,2)");
        b.Ignore(i => i.Total);
    }
}

public class PedidoCompraConfiguration : IEntityTypeConfiguration<PedidoCompra>
{
    public void Configure(EntityTypeBuilder<PedidoCompra> e)
    {
        e.HasKey(p => p.Id);
        e.ToTable("PedidosCompra");
        e.Property(p => p.Numero).HasMaxLength(20).IsRequired();
        e.Property(p => p.FornecedorNome).HasMaxLength(200).IsRequired();
        e.HasMany(p => p.Itens)
            .WithOne(i => i.PedidoCompra)
            .HasForeignKey(i => i.PedidoCompraId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // 🔧 FIX COMPRAS: Total é propriedade computada (=> Itens.Sum(...))
        // Sem isso, o EF cria uma coluna "Total" no banco e salva 0.
        e.Ignore(p => p.Total);
    }
}
 
public class PedidoCompraItemConfiguration : IEntityTypeConfiguration<PedidoCompraItem>
{
    public void Configure(EntityTypeBuilder<PedidoCompraItem> e)
    {
        e.HasKey(i => i.Id);
        e.ToTable("PedidoCompraItens");
        e.Property(p => p.Quantidade).HasColumnType("decimal(10,3)");
        e.Property(p => p.PrecoUnitario).HasColumnType("decimal(18,2)");
        e.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // 🔧 FIX COMPRAS: Total é propriedade computada (=> Quantidade * PrecoUnitario)
        e.Ignore(i => i.Total);
    }
}