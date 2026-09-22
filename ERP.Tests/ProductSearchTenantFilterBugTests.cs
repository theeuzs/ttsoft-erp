// ERP.Tests/ProductSearchTenantFilterBugTests.cs
using ERP.Domain.Entities;
using ERP.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests;

/// <summary>
/// Regressão do bug achado testando a Fase C (sem relação com a migração de
/// Cliente — Produto não foi tocado nesta entrega): busca de produto
/// (PDV/NotaAvulsa/Compras/HistoricoCompras/HistoricoVendas, todos via
/// IProductService.SearchAsync) sumia com o produto inteiro quando o
/// CategoryId apontava pra uma categoria de OUTRO tenant.
///
/// Causa: ProductRepository.SearchAsync faz .Include(p => p.Category), e
/// Category tem HasQueryFilter global de TenantId — mesmo Category sendo
/// navegação OPCIONAL (CategoryId é Guid?). Sem AsSplitQuery(), o EF Core
/// (query única) pode aplicar o filtro da entidade incluída de um jeito que
/// derruba a linha do PAI inteira, em vez de só devolver Category = null.
/// Usa SQLite real (não InMemory) porque esse comportamento é de tradução
/// de JOIN — o provider InMemory não reproduz.
/// </summary>
public class ProductSearchTenantFilterBugTests
{
    [Fact(DisplayName =
        "SearchAsync — produto com CategoryId de OUTRO tenant continua aparecendo na busca " +
        "(reprodução exata do achado: \"PARAFUSO PHILLIPS 4.0X40\" sumia buscando \"PHILLIPS\")")]
    public async Task SearchAsync_CategoriaDeOutroTenant_NaoEsconceOProduto()
    {
        var tenantDaLoja  = Guid.NewGuid();
        var tenantDeFora  = Guid.NewGuid(); // simula categoria "vazada" de outro tenant
        var categoriaDeFora = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tenantDaLoja, ctx =>
        {
            // Categoria de um tenant DIFERENTE do produto — cenário real
            // encontrado (Category.TenantId != Product.TenantId).
            ctx.Database.ExecuteSqlRaw(
                "INSERT INTO Categories (Id, Name, TenantId, IsDeleted, CreatedAt) " +
                "VALUES ({0}, 'Parafusos', {1}, 0, {2})",
                categoriaDeFora, tenantDeFora, DateTime.UtcNow);

            ctx.Products.Add(new Product
            {
                Id         = Guid.NewGuid(),
                Name       = "PARAFUSO PHILLIPS 4.0X40",
                CategoryId = categoriaDeFora,
                TenantId   = tenantDaLoja,
                IsActive   = true,
                Unit       = "UN",
                SalePrice  = 0.40m,
                Stock      = 500
            });
        });

        var repo = new ProductRepository(db);

        // A palavra mais óbvia do nome — é justamente essa que sumia.
        var resultado = await repo.SearchAsync("PHILLIPS");

        resultado.Should().ContainSingle(p => p.Name == "PARAFUSO PHILLIPS 4.0X40",
            "o produto pertence ao tenant certo — só a categoria referenciada é de outro tenant; " +
            "isso nunca deveria esconder o produto da busca");
    }

    [Fact(DisplayName = "SearchAsync — produto SEM categoria (CategoryId null) sempre apareceu, continua aparecendo")]
    public async Task SearchAsync_SemCategoria_ContinuaFuncionando()
    {
        var tenantDaLoja = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tenantDaLoja, ctx =>
        {
            ctx.Products.Add(new Product
            {
                Id         = Guid.NewGuid(),
                Name       = "PRODUTO SEM CATEGORIA",
                CategoryId = null,
                TenantId   = tenantDaLoja,
                IsActive   = true,
                Unit       = "UN",
                SalePrice  = 10m,
                Stock      = 10
            });
        });

        var repo = new ProductRepository(db);
        var resultado = await repo.SearchAsync("SEM CATEGORIA");

        resultado.Should().ContainSingle();
    }
}