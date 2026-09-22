// ERP.Tests/ProductSearchTests.cs
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
/// Bug real (achado testando a Fase C, sem relação com a migração de
/// Cliente): busca de produto no PDV sumia com produtos onde a palavra
/// buscada não era o INÍCIO do nome — "phillips" e "4.0X40" não achavam
/// "PARAFUSO PHILLIPS 4.0X40", só "parafuso" achava.
///
/// Causa raiz confirmada via ToQueryString() contra o Azure SQL real: o
/// provider SQL Server do EF Core reaproveita o parâmetro de padrão LIKE
/// pela identidade da variável C# capturada (`word`), não por qual método
/// LINQ gerou cada LIKE. Usar a MESMA variável em .Contains() (Name) e
/// .StartsWith() (Barcode/SKU) na mesma query fazia o Contains herdar o
/// padrão do StartsWith (só sufixo).
///
/// LIMITAÇÃO HONESTA: esse comportamento é do provider SQL Server
/// especificamente. O provider SQLite usado aqui NÃO reproduz o bug (o
/// código antigo, com a mistura Contains+StartsWith problemática, já
/// passava nesses testes antes do fix) — não existe fixture de SQL Server
/// real neste projeto de testes. Ou seja: estes testes confirmam que a
/// busca continua funcionando corretamente (comportamento esperado), mas
/// NÃO teriam pego o bug original sozinhos. A verificação de que o bug
/// foi corrigido de verdade é manual, contra o Azure SQL real (PDV: buscar
/// "phillips", "4.0X40" e o nome completo).
/// </summary>
public class ProductSearchTests
{
    private static async Task<Product?> BuscarUm(AppDbContextComProduto ctx, string termo)
    {
        var repo = new ProductRepository(ctx.Db);
        var resultado = await repo.SearchAsync(termo);
        return resultado.SingleOrDefault();
    }

    [Fact(DisplayName = "SearchAsync — acha produto por palavra no MEIO do nome, não só no início")]
    public async Task SearchAsync_PalavraNoMeioDoNome_Encontra()
    {
        using var ctx = ComProduto("PARAFUSO PHILLIPS 4.0X40");

        (await BuscarUm(ctx, "PHILLIPS"))?.Name.Should().Be("PARAFUSO PHILLIPS 4.0X40");
        (await BuscarUm(ctx, "4.0X40"))?.Name.Should().Be("PARAFUSO PHILLIPS 4.0X40");
        (await BuscarUm(ctx, "PARAFUSO PHILLIPS 4.0X40"))?.Name.Should().Be("PARAFUSO PHILLIPS 4.0X40");
    }

    [Fact(DisplayName = "SearchAsync — continua achando por palavra no início do nome (caso que já funcionava)")]
    public async Task SearchAsync_PalavraNoInicioDoNome_ContinuaEncontrando()
    {
        using var ctx = ComProduto("PARAFUSO PHILLIPS 4.0X40");
        (await BuscarUm(ctx, "PARAFUSO"))?.Name.Should().Be("PARAFUSO PHILLIPS 4.0X40");
    }

    [Fact(DisplayName = "SearchAsync — nome com % ou _ no meio não quebra o padrão LIKE (precisa de escape manual)")]
    public async Task SearchAsync_NomeComCoringaLike_NaoQuebraABusca()
    {
        using var ctx = ComProduto("CABO 50% FLEX 2,5MM");
        (await BuscarUm(ctx, "50%"))?.Name.Should().Be("CABO 50% FLEX 2,5MM");

        using var ctx2 = ComProduto("PARAFUSO_ESPECIAL");
        (await BuscarUm(ctx2, "PARAFUSO_ESPECIAL"))?.Name.Should().Be("PARAFUSO_ESPECIAL");
    }

    [Fact(DisplayName = "SearchAsync — produto sem categoria continua aparecendo")]
    public async Task SearchAsync_SemCategoria_ContinuaFuncionando()
    {
        using var ctx = ComProduto("PRODUTO SEM CATEGORIA", categoriaId: null);
        (await BuscarUm(ctx, "SEM CATEGORIA"))?.Name.Should().Be("PRODUTO SEM CATEGORIA");
    }

    // ── helper ──────────────────────────────────────────────────────────
    private static AppDbContextComProduto ComProduto(string nome, Guid? categoriaId = default)
    {
        var tenantId = Guid.NewGuid();
        var ctx = TestDbSqlite.Create(tenantId, db =>
        {
            db.Products.Add(new Product
            {
                Id         = Guid.NewGuid(),
                Name       = nome,
                CategoryId = categoriaId == default ? null : categoriaId,
                TenantId   = tenantId,
                IsActive   = true,
                Unit       = "UN",
                SalePrice  = 1m,
                Stock      = 1
            });
        });
        return new AppDbContextComProduto(ctx);
    }
}

/// <summary>Wrapper só pra dar nome legível ao AppDbContext nos testes acima e
/// permitir `using var ctx = ...` descartando o contexto certo.</summary>
internal sealed class AppDbContextComProduto : IDisposable
{
    public ERP.Persistence.Context.AppDbContext Db { get; }
    public AppDbContextComProduto(ERP.Persistence.Context.AppDbContext db) => Db = db;
    public void Dispose() => Db.Dispose();
}
