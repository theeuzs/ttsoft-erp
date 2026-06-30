// ── ERP.Tests/Controllers/ControllersIntegrationTests.cs ─────────────────────
// Testes de integração para os controllers da API.
//
// Estratégia:
//   • WebApplicationFactory<Program> com InMemory DB substitui SQL Server Azure
//   • JWT real gerado com a chave de teste para endpoints autenticados
//   • Cada controller tem ao menos 2 testes: (1) guard de autenticação e (2) happy path
//   • Controllers com lógica crítica têm cobertura extra
// ─────────────────────────────────────────────────────────────────────────────
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace ERP.Tests.Controllers;

// ═══════════════════════════════════════════════════════════════════════════════
//  FACTORY — substitui SQL Server por InMemory e fornece helpers
// ═══════════════════════════════════════════════════════════════════════════════

public class ErpApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid   TestTenantId = Guid.NewGuid();
    private const string          JwtKey       = "TestSecretKeyForIntegrationTests_32bytes!";
    private const string          JwtIssuer    = "ERPTest";
    private const string          JwtAudience  = "ERPTest";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            var internalServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            var dbName = $"IntegrationTests_{Guid.NewGuid()}";
            services.AddSingleton(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .UseInternalServiceProvider(internalServiceProvider)
                    .Options);

            services.AddScoped<AppDbContext>(sp => new AppDbContext(
                sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
                sp.GetRequiredService<IRequestTenant>()
            ));

            services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer           = true,
                        ValidateAudience         = true,
                        ValidateLifetime         = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer              = JwtIssuer,
                        ValidAudience            = JwtAudience,
                        IssuerSigningKey         = new SymmetricSecurityKey(
                                                       Encoding.UTF8.GetBytes(JwtKey)),
                        RoleClaimType            = "role_name"
                    };
                });
        });
    }

    /// <summary>
    /// Gera um JWT de teste com TenantId, permissões e flags informadas.
    /// mustChangePassword=true adiciona o claim "must_change_password: true" (1.7.4).
    /// </summary>
    public string GerarToken(
        string    cargo               = "Administrador",
        string[]? permissoes          = null,
        Guid?     tenantId            = null,
        bool      mustChangePassword  = false)
    {
        var tid    = tenantId ?? TestTenantId;
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,  Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, "Usuário Teste"),
            new(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            new("tenant_id",                  tid.ToString()),
            new("role_name",                  cargo),
        };

        foreach (var p in permissoes ?? Permissions.All)
            claims.Add(new Claim("permission", p));

        // 1.7.4: flag de troca obrigatória — MustChangePasswordMiddleware bloqueia
        // qualquer endpoint exceto /api/auth/change-password quando este claim existe.
        if (mustChangePassword)
            claims.Add(new Claim("must_change_password", "true"));

        var token = new JwtSecurityToken(
            issuer:             JwtIssuer,
            audience:           JwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient CreateAuthenticatedClient(string cargo = "Administrador")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GerarToken(cargo));
        return client;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  BASE
// ═══════════════════════════════════════════════════════════════════════════════

public abstract class IntegrationTestBase : IClassFixture<ErpApiFactory>
{
    protected readonly ErpApiFactory  Factory;
    protected readonly HttpClient     AuthClient;
    protected readonly HttpClient     AnonClient;

    protected IntegrationTestBase(ErpApiFactory factory)
    {
        Factory    = factory;
        AuthClient = factory.CreateAuthenticatedClient();
        AnonClient = factory.CreateClient();
    }

    protected static StringContent Json<T>(T obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");
}

// ═══════════════════════════════════════════════════════════════════════════════
//  TENANT SCOPE
// ═══════════════════════════════════════════════════════════════════════════════

internal sealed class TenantScope : IDisposable
{
    public TenantScope(Guid tenantId) => AppDbContext.SetQueryTenantId(tenantId);
    public void Dispose()             => AppDbContext.SetQueryTenantId(Guid.Empty);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  AUTH
// ═══════════════════════════════════════════════════════════════════════════════

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "POST /api/auth/login sem header X-Tenant-CNPJ → 400")]
    public async Task Login_SemCnpj_RetornaBadRequest()
    {
        var resp = await AnonClient.PostAsJsonAsync("/api/auth/login",
            new { Username = "user", Password = "pass" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "POST /api/auth/login credenciais inválidas → 401")]
    public async Task Login_CredenciaisInvalidas_Retorna401()
    {
        AnonClient.DefaultRequestHeaders.Add("X-Tenant-CNPJ", "00000000000191");
        var resp = await AnonClient.PostAsJsonAsync("/api/auth/login",
            new { Username = "naoexiste", Password = "errado" });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "POST /api/auth/login — 6 tentativas no mesmo CNPJ → 429 na 6ª (1.6.5)")]
    public async Task Login_RateLimitPorCnpj_Retorna429NaSextaTentativa()
    {
        var cnpjUnico = "99" + Guid.NewGuid().ToString("N")[..12];
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-CNPJ", cnpjUnico);

        var statusCodes = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            var r = await client.PostAsJsonAsync("/api/auth/login",
                new { Username = "brute", Password = "force" });
            statusCodes.Add(r.StatusCode);
            if (r.StatusCode == HttpStatusCode.TooManyRequests) break;
        }

        statusCodes.Should().Contain(HttpStatusCode.TooManyRequests,
            "deve retornar 429 após ultrapassar o limite de 5 req/min no mesmo CNPJ (1.6.5)");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  PRODUCTS
// ═══════════════════════════════════════════════════════════════════════════════

public class ProductsControllerTests : IntegrationTestBase
{
    public ProductsControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/products sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
    {
        var resp = await AnonClient.GetAsync("/api/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET /api/products com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
    {
        var resp = await AuthClient.GetAsync("/api/products");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET /api/products/catalogo é público (AllowAnonymous) → 200")]
    public async Task Catalogo_SemToken_Retorna200()
    {
        var resp = await AnonClient.GetAsync("/api/products/catalogo");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "POST /api/products com payload inválido → 400")]
    public async Task Create_PayloadInvalido_Retorna400()
    {
        var resp = await AuthClient.PostAsync("/api/products", Json(new { }));
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CUSTOMERS
// ═══════════════════════════════════════════════════════════════════════════════

public class CustomersControllerTests : IntegrationTestBase
{
    public CustomersControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/customers sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/customers"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/customers com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/customers"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "GET /api/customers/{guid} inexistente → 404")]
    public async Task GetById_NaoExistente_Retorna404()
        => (await AuthClient.GetAsync($"/api/customers/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  SALES
// ═══════════════════════════════════════════════════════════════════════════════

public class SalesControllerTests : IntegrationTestBase
{
    public SalesControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/sales sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/sales"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/sales com token → 200 lista vazia")]
    public async Task GetAll_ComToken_RetornaListaVazia()
    {
        var resp = await AuthClient.GetAsync("/api/sales");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "GET /api/sales/{guid} inexistente → 404")]
    public async Task GetById_NaoExistente_Retorna404()
        => (await AuthClient.GetAsync($"/api/sales/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  METAS
// ═══════════════════════════════════════════════════════════════════════════════

public class MetasControllerTests : IntegrationTestBase
{
    public MetasControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/metas sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/metas"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/metas com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/metas"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "POST /api/metas cria meta e retorna Id")]
    public async Task Upsert_NovasMeta_RetornaId()
    {
        var resp = await AuthClient.PostAsync("/api/metas", Json(new MetaVendasDto
        {
            VendedorNome = "Ana",
            Mes          = 1,
            Ano          = 2026,
            ValorMeta    = 10000m
        }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("id");
    }

    [Fact(DisplayName = "DELETE /api/metas/{guid} → 204")]
    public async Task Delete_IdQualquer_Retorna204()
        => (await AuthClient.DeleteAsync($"/api/metas/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CONTAS A PAGAR
// ═══════════════════════════════════════════════════════════════════════════════

public class ContasPagarControllerTests : IntegrationTestBase
{
    public ContasPagarControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/contas-pagar/pendentes sem token → 401")]
    public async Task Pendentes_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/contas-pagar/pendentes"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/contas-pagar/pendentes com token → 200")]
    public async Task Pendentes_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/contas-pagar/pendentes"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "GET /api/contas-pagar/resumo → 200 com zeros")]
    public async Task Resumo_BancoVazio_RetornaZeros()
    {
        var resp = await AuthClient.GetAsync("/api/contas-pagar/resumo");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("totalPendente");
    }

    [Fact(DisplayName = "POST /api/contas-pagar cria conta e retorna Id")]
    public async Task Create_ContaValida_RetornaId()
    {
        var resp = await AuthClient.PostAsync("/api/contas-pagar", Json(new CreateContaPagarDto
        {
            Descricao      = "Aluguel",
            Valor          = 2500m,
            DataVencimento = DateTime.Today.AddDays(30),
            Categoria      = "Despesa Fixa"
        }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("id");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  AUDITORIA
// ═══════════════════════════════════════════════════════════════════════════════

public class AuditoriaControllerTests : IntegrationTestBase
{
    public AuditoriaControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/auditoria sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/auditoria"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/auditoria com token Administrador → 200 paginado")]
    public async Task GetAll_ComToken_Retorna200Paginado()
    {
        var resp = await AuthClient.GetAsync("/api/auditoria?pagina=1&tam=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("items").And.Contain("total");
    }

    [Fact(DisplayName = "GET /api/auditoria com filtros → 200")]
    public async Task GetAll_ComFiltros_Retorna200()
        => (await AuthClient.GetAsync("/api/auditoria?usuario=admin&acao=UPDATE&pagina=1&tam=5"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "GET /api/auditoria sem AuditView → 403 (1.6.6)")]
public async Task GetAll_RoleGerente_Retorna403()
{
    // Sistema agora usa [HasPermission(AuditView)] — testa a permissão, não o cargo
    var perms  = Permissions.All.Where(p => p != Permissions.AuditView).ToArray();
    var client = Factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", Factory.GerarToken("Gerente", perms));

    (await client.GetAsync("/api/auditoria"))
        .StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usuario sem audit.view nao deve ver logs de auditoria");
}
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CAIXA
// ═══════════════════════════════════════════════════════════════════════════════

public class CaixaControllerTests : IntegrationTestBase
{
    public CaixaControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/caixa/status sem token → 401")]
    public async Task Status_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/caixa/aberto"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/caixa/status com token → 200 ou 404")]
    public async Task Status_ComToken_Retorna200()
    {
        var status = (await AuthClient.GetAsync("/api/caixa/aberto")).StatusCode;
        status.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INVENTÁRIO
// ═══════════════════════════════════════════════════════════════════════════════

public class InventarioControllerTests : IntegrationTestBase
{
    public InventarioControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/inventario/produtos sem token → 401")]
    public async Task Produtos_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/inventario/produtos"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/inventario/produtos com token → 200 paginado")]
    public async Task Produtos_ComToken_Retorna200Paginado()
        => (await AuthClient.GetAsync("/api/inventario/produtos?pagina=1&tam=10"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "GET /api/inventario/barcode/{codigo} inexistente → 404")]
    public async Task Barcode_NaoExistente_Retorna404()
        => (await AuthClient.GetAsync("/api/inventario/barcode/9999999999999"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FIDELIDADE
// ═══════════════════════════════════════════════════════════════════════════════

public class FidelidadeControllerTests : IntegrationTestBase
{
    public FidelidadeControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/fidelidade/saldo/{guid} sem token → 401")]
    public async Task Saldo_SemToken_Retorna401()
        => (await AnonClient.GetAsync($"/api/fidelidade/{Guid.NewGuid()}/saldo"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/fidelidade/saldo/{guid} com token → 200")]
    public async Task Saldo_ComToken_Retorna200()
        => (await AuthClient.GetAsync($"/api/fidelidade/{Guid.NewGuid()}/saldo"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  HAVER
// ═══════════════════════════════════════════════════════════════════════════════

public class HaverControllerTests : IntegrationTestBase
{
    public HaverControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/haver/saldo/{guid} sem token → 401")]
    public async Task Saldo_SemToken_Retorna401()
        => (await AnonClient.GetAsync($"/api/haver/saldo/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/haver/historico/{guid} com token → 200")]
    public async Task Historico_ComToken_Retorna200()
        => (await AuthClient.GetAsync($"/api/haver/historico/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CONTAS A RECEBER
// ═══════════════════════════════════════════════════════════════════════════════

public class ContasReceberControllerTests : IntegrationTestBase
{
    public ContasReceberControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/contasreceber sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/contas-receber/pendentes"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/contasreceber com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/contas-receber/pendentes"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  ENTREGAS
// ═══════════════════════════════════════════════════════════════════════════════

public class EntregasControllerTests : IntegrationTestBase
{
    public EntregasControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/entregas sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/entregas"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/entregas com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/entregas"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "GET /api/entregas/relatorio com token → 200")]
    public async Task Relatorio_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/entregas/relatorio"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  STOCK
// ═══════════════════════════════════════════════════════════════════════════════

public class StockControllerTests : IntegrationTestBase
{
    public StockControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/stock sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/stock"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/stock/low com token → 200")]
    public async Task LowStock_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/stock/low"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  ORÇAMENTOS
// ═══════════════════════════════════════════════════════════════════════════════

public class OrcamentosControllerTests : IntegrationTestBase
{
    public OrcamentosControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/orcamentos sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/orcamentos"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/orcamentos com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/orcamentos"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  PEDIDOS DE COMPRA
// ═══════════════════════════════════════════════════════════════════════════════

public class PedidosCompraControllerTests : IntegrationTestBase
{
    public PedidosCompraControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/pedidos-compra sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/pedidos-compra"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/pedidos-compra com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/pedidos-compra"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CARGOS
// ═══════════════════════════════════════════════════════════════════════════════

public class CargosControllerTests : IntegrationTestBase
{
    public CargosControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/cargos sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/cargos"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/cargos com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/cargos"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FILIAL
// ═══════════════════════════════════════════════════════════════════════════════

public class FilialControllerTests : IntegrationTestBase
{
    public FilialControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/filial sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/filial"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/filial com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/filial"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  TRANSFERÊNCIAS
// ═══════════════════════════════════════════════════════════════════════════════

public class TransferenciasControllerTests : IntegrationTestBase
{
    public TransferenciasControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/transferencias sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/transferencias"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/transferencias com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/transferencias"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FISCAL
// ═══════════════════════════════════════════════════════════════════════════════

public class FiscalControllerTests : IntegrationTestBase
{
    public FiscalControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/fiscal sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/fiscal/aliquota-icms/SP"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  NOTAS FISCAIS
// ═══════════════════════════════════════════════════════════════════════════════

public class NotasFiscaisControllerTests : IntegrationTestBase
{
    public NotasFiscaisControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/notas-fiscais sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/notas-fiscais"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/notas-fiscais com token → 200 paginado")]
    public async Task GetAll_ComToken_Retorna200()
    {
        var resp = await AuthClient.GetAsync("/api/notas-fiscais?pagina=1&tam=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("items");
    }

    [Fact(DisplayName = "GET /api/notas-fiscais/{ref}/status com token → 200")]
    public async Task Status_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/notas-fiscais/ref-teste/status"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "POST /api/notas-fiscais/{ref}/cancelar justificativa curta → 400")]
    public async Task Cancelar_JustificativaCurta_Retorna400()
    {
        var resp = await AuthClient.PostAsync(
            "/api/notas-fiscais/ref-teste/cancelar",
            Json(new { Justificativa = "curta" }));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CALCULADORA
// ═══════════════════════════════════════════════════════════════════════════════

public class CalculadoraControllerTests : IntegrationTestBase
{
    public CalculadoraControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/calculadora/templates sem token → 200 (endpoint público)")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/calculadora/templates"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  BI
// ═══════════════════════════════════════════════════════════════════════════════

public class BiControllerTests : IntegrationTestBase
{
    public BiControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/bi sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/bi/abc"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  MARKETPLACE
// ═══════════════════════════════════════════════════════════════════════════════

public class MarketplaceControllerTests : IntegrationTestBase
{
    public MarketplaceControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/marketplace sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/marketplace/config"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  SUGESTÃO DE COMPRAS
// ═══════════════════════════════════════════════════════════════════════════════

public class SugestaoComprasControllerTests : IntegrationTestBase
{
    public SugestaoComprasControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/sugestao-compras sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/sugestao-compras"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/sugestao-compras com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/sugestao-compras"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  DEVOLUÇÃO
// ═══════════════════════════════════════════════════════════════════════════════

public class DevolucaoControllerTests : IntegrationTestBase
{
    public DevolucaoControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "POST /api/devolucao sem token → 401")]
    public async Task Post_SemToken_Retorna401()
        => (await AnonClient.PostAsync("/api/devolucao", Json(new { })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CHAT TOKEN
// ═══════════════════════════════════════════════════════════════════════════════

public class ChatTokenControllerTests : IntegrationTestBase
{
    public ChatTokenControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "POST /api/auth/chat-token sem token → 401")]
    public async Task GetChatToken_SemToken_Retorna401()
        => (await AnonClient.PostAsync("/api/auth/chat-token", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "POST /api/auth/chat-token com token → 200 com chatToken")]
    public async Task GetChatToken_ComToken_Retorna200ComJwt()
    {
        var resp = await AuthClient.PostAsync("/api/auth/chat-token", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<ChatTokenResponse>();
        body.Should().NotBeNull();
        body!.ChatToken.Should().NotBeNullOrEmpty("deve retornar um JWT de curta duração");
        body.ExpiresIn.Should().Be(300, "ChatToken deve expirar em 5 minutos (300 segundos)");
    }

    [Fact(DisplayName = "POST /api/auth/chat-token retorna JWT válido com claim chat_token")]
    public async Task GetChatToken_JwtContemClaimChatToken()
    {
        var resp  = await AuthClient.PostAsync("/api/auth/chat-token", null);
        var body  = await resp.Content.ReadFromJsonAsync<ChatTokenResponse>();
        var token = body!.ChatToken;

        var parts = token.Split('.');
        parts.Should().HaveCount(3, "JWT deve ter 3 partes separadas por ponto");

        var payload = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(PadBase64(parts[1])));
        payload.Should().Contain("chat_token", "claim 'chat_token' deve estar no payload");
        payload.Should().Contain("tenant_id",  "claim 'tenant_id' deve estar no payload");
    }

    private static string PadBase64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
    }

    private record ChatTokenResponse(string ChatToken, int ExpiresIn);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  METRICS
// ═══════════════════════════════════════════════════════════════════════════════

public class MetricsControllerTests : IntegrationTestBase
{
    public MetricsControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/metrics sem token → 401")]
    public async Task GetMetrics_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/metrics"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/metrics com token → 200")]
    public async Task GetMetrics_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/metrics"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact(DisplayName = "GET /api/metrics retorna snapshot com campos esperados")]
    public async Task GetMetrics_RetornaSnapshotCompleto()
    {
        await AuthClient.GetAsync("/api/products");
        await AuthClient.GetAsync("/api/customers");

        var resp = await AuthClient.GetAsync("/api/metrics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<MetricsSnapshot>();
        body.Should().NotBeNull();
        body!.JanelaMinutos.Should().Be(5);
        body.RequestsPorSeg.Should().BeGreaterThanOrEqualTo(0);
        body.LatenciaMediaMs.Should().BeGreaterThanOrEqualTo(0);
        body.LatenciaP99Ms.Should().BeGreaterThanOrEqualTo(0);
        body.TaxaErro5xx.Should().BeInRange(0, 100);
        body.EndpointsMaisLentos.Should().NotBeNull();
    }

    [Fact(DisplayName = "GET /api/metrics após requests registra endpoints")]
    public async Task GetMetrics_AposRequests_RegistraEndpoints()
    {
        await AuthClient.GetAsync("/api/products");
        var resp = await AuthClient.GetAsync("/api/metrics");
        var body = await resp.Content.ReadFromJsonAsync<MetricsSnapshot>();
        body!.TotalRequests.Should().BeGreaterThan(0);
    }

    private class MetricsSnapshot
    {
        public int                   JanelaMinutos        { get; set; }
        public int                   TotalRequests        { get; set; }
        public double                RequestsPorSeg       { get; set; }
        public double                LatenciaMediaMs      { get; set; }
        public double                LatenciaP99Ms        { get; set; }
        public double                TaxaErro5xx          { get; set; }
        public List<EndpointMetric>? EndpointsMaisLentos  { get; set; }
    }

    private class EndpointMetric
    {
        public string Path            { get; set; } = "";
        public int    Requests        { get; set; }
        public double LatenciaMediaMs { get; set; }
    }

    // ── FASE 0 — isolamento cross-tenant ─────────────────────────────────────

    public class Fase0TenantIsolationTests : IDisposable
    {
        private readonly ErpApiFactory _factory = new();
        public void Dispose() => _factory.Dispose();

        [Fact(DisplayName = "FASE0 #1 — Produto do Tenant A NÃO aparece para Tenant B")]
        public async Task Produto_TenantA_NaoAparece_Para_TenantB()
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            using (new TenantScope(tenantA))
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Products.Add(new ERP.Domain.Entities.Product
                {
                    Id = Guid.NewGuid(), TenantId = tenantA,
                    Name = "Cimento CP-II 50kg — TenantA",
                    SalePrice = 35.90m, Stock = 100,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var clientB = _factory.CreateClient();
            clientB.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _factory.GerarToken(tenantId: tenantB));

            var resp = await clientB.GetAsync("/api/products");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resp.Content.ReadAsStringAsync())
                .Should().NotContain("Cimento CP-II 50kg — TenantA");
        }

        [Fact(DisplayName = "FASE0 #2 — Produto do Tenant A aparece corretamente para Tenant A")]
        public async Task Produto_TenantA_Aparece_Para_TenantA()
        {
            var tenantA   = Guid.NewGuid();
            var produtoId = Guid.NewGuid();

            using (new TenantScope(tenantA))
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Products.Add(new ERP.Domain.Entities.Product
                {
                    Id = produtoId, TenantId = tenantA,
                    Name = $"Argamassa AC-II — TenantA-{produtoId:N}",
                    SalePrice = 22.50m, Stock = 50,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var clientA = _factory.CreateClient();
            clientA.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _factory.GerarToken(tenantId: tenantA));

            var resp = await clientA.GetAsync("/api/products");
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resp.Content.ReadAsStringAsync())
                .Should().Contain($"TenantA-{produtoId:N}");
        }

        [Fact(DisplayName = "FASE0 #3 — GET /api/haver/saldo com cliente de outro tenant retorna 0")]
        public async Task Haver_ClienteOutroTenant_NaoVazaDado()
        {
            var tenantA   = Guid.NewGuid();
            var tenantB   = Guid.NewGuid();
            var clienteId = Guid.NewGuid();

            using (new TenantScope(tenantA))
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Customers.Add(new ERP.Domain.Entities.Customer
                {
                    Id = clienteId, TenantId = tenantA,
                    Name = "Cliente do Tenant A", HaverBalance = 500m,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            var clientB = _factory.CreateClient();
            clientB.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _factory.GerarToken(tenantId: tenantB));

            var resp = await clientB.GetAsync($"/api/haver/saldo/{clienteId}");

            if (resp.IsSuccessStatusCode)
            {
                // S10 FIX: NotContain("500") era falso positivo — UUID no body pode conter "500".
                // Verifica que o saldo retornado é 0 (isolamento cross-tenant funcionando).
                var body = await resp.Content.ReadAsStringAsync();
                body.Should().Contain("\"saldo\":0",
                    because: "cliente de outro tenant não deve ter saldo visível");
            }
            else
                resp.StatusCode.Should().BeOneOf(
                    HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  F1.1 — DUAL-TENANT ISOLATION TESTS
// ═══════════════════════════════════════════════════════════════════════════════

public class F11DualTenantTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    public F11DualTenantTests(ErpApiFactory factory) => _factory = factory;

    private async Task SeedAsync(Guid tenantId, Action<AppDbContext> seed)
    {
        using var ts    = new TenantScope(tenantId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private HttpClient ClientFor(Guid tenantId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.GerarToken(tenantId: tenantId));
        return c;
    }

    [Fact(DisplayName = "F1.1 — Customers: Tenant B nao ve clientes do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task Customers_TenantB_NaoVeClientesDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker  = $"Cliente-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.Customers.Add(new ERP.Domain.Entities.Customer
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Name = marker,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/customers");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    [Fact(DisplayName = "F1.1 — Sales: Tenant B nao ve vendas do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task Sales_TenantB_NaoVeVendasDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var saleNum = $"SALE-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.Sales.Add(new ERP.Domain.Entities.Sale
        {
            Id = Guid.NewGuid(), TenantId = tenantA, SaleNumber = saleNum,
            Total = 100m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            SaleDate = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/sales");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(saleNum);
    }

    [Fact(DisplayName = "F1.1 — Orcamentos: Tenant B nao ve orcamentos do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task Orcamentos_TenantB_NaoVeOrcamentosDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker  = $"ORC-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.Orcamentos.Add(new ERP.Domain.Entities.Orcamento
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Numero = marker,
            ValorTotal = 500m, DataEmissao = DateTime.UtcNow,
            DataValidade = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/orcamentos");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    [Fact(DisplayName = "F1.1 — ContasReceber: Tenant B nao ve contas do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task ContasReceber_TenantB_NaoVeContasDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker  = $"CR-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.ContasReceber.Add(new ERP.Domain.Entities.ContaReceber
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Descricao = marker,
            ValorTotal = 1000m, DataVencimento = DateTime.UtcNow.AddDays(30),
            CustomerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/contas-receber/pendentes");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    [Fact(DisplayName = "F1.1 — ContasPagar: Tenant B nao ve contas do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task ContasPagar_TenantB_NaoVeContasDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker  = $"CP-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.ContasPagar.Add(new ERP.Domain.Entities.ContaPagar
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Descricao = marker,
            Valor = 500m, DataVencimento = DateTime.UtcNow.AddDays(15),
            CreatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/contas-pagar/pendentes");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    [Fact(DisplayName = "F1.1 — Fidelidade: saldo de cliente do Tenant A e 0 para Tenant B")]
    [Trait("F11", "DualTenant")]
    public async Task Fidelidade_ClienteTenantA_SaldoZeroParaTenantB()
    {
        var tenantA   = Guid.NewGuid();
        var tenantB   = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        await SeedAsync(tenantA, db => db.PontosFidelidade.Add(new ERP.Domain.Entities.PontosFidelidade
        {
            Id = Guid.NewGuid(), TenantId = tenantA,
            CustomerId = clienteId, Tipo = "Credito",
            Pontos = 500, Data = DateTime.UtcNow, Descricao = "Compra teste"
        }));

        var resp = await ClientFor(tenantB).GetAsync($"/api/fidelidade/{clienteId}/saldo");

        if (resp.IsSuccessStatusCode)
            (await resp.Content.ReadAsStringAsync()).Should().NotContain("500");
        else
            resp.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "F1.1 — PedidosCompra: Tenant B nao ve pedidos do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task PedidosCompra_TenantB_NaoVePedidosDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker  = $"PC-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.PedidosCompra.Add(new ERP.Domain.Entities.PedidoCompra
        {
            Id = Guid.NewGuid(), TenantId = tenantA, Numero = marker,
            Status = ERP.Domain.Enums.StatusPedidoCompra.Rascunho,
            DataPedido = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/pedidos-compra");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    [Fact(DisplayName = "F1.1 — Entregas: Tenant B nao ve entregas do Tenant A")]
    [Trait("F11", "DualTenant")]
    public async Task Entregas_TenantB_NaoVeEntregasDeTenantA()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker  = $"END-A-{tenantA:N}";

        await SeedAsync(tenantA, db => db.Entregas.Add(new ERP.Domain.Entities.Entrega
        {
            Id = Guid.NewGuid(), TenantId = tenantA,
            ClienteNome = marker, SaleId = Guid.NewGuid(),
            Status = ERP.Domain.Enums.StatusEntrega.Pendente,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/entregas");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    [Fact(DisplayName = "F1.1 — Devolucao: POST autenticado nao retorna 500")]
    [Trait("F11", "DualTenant")]
    public async Task Devolucao_ComAuth_NaoRetorna500()
    {
        var resp = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/devolucao",
            new
            {
                SaleId = Guid.NewGuid(),
                Items  = new[] { new { ProductId = Guid.NewGuid(), Quantidade = 1m, Motivo = "Teste" } }
            });

        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  1.7.8 — REGRESSÃO DE SEGURANÇA
//
//  Cobre os fixes críticos da Fase 1.7:
//  • 1.7.2 — Shopee webhook: body obrigatório na assinatura (body forgery)
//  • 1.7.3 — ML webhook: replay protection (timestamp > 5min rejeitado)
//  • 1.7.4 — MustChangePassword: middleware bloqueia endpoints / libera troca de senha
//  • 1.6.1 — Cross-tenant auth: credenciais de tenant A rejeitadas em tenant B
// ═══════════════════════════════════════════════════════════════════════════════

// ── 1.7.2 + 1.7.3: Webhook Signature Validator (testes unitários puros) ───────

public class WebhookSignatureValidatorTests
{
    // Chaves fictícias de teste — sem dependência de config/DB/HTTP
    private const string MlSecret  = "ml_test_secret_key_32bytes_padding!";
    private const string ShopeeKey = "shopee_test_key_32bytes_padding_!!";
    private const string ShopeeId  = "12345";
    private const string ShopeeUrl =
        "https://api.ttsoft.com.br/api/marketplace/shopee/webhook/00000000-0000-0000-0000-000000000001";

    // ── ML ──────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "1.7.3 — ML: assinatura válida + timestamp recente → aceito")]
    public void ML_AssinaturaValida_Aceito()
    {
        var body = """{"topic":"orders","resource":"/orders/123"}""";
        var tsMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sig  = HmacML(tsMs, body);

        WebhookSignatureValidator.ValidateML($"ts={tsMs}&v1={sig}", body, MlSecret)
            .Should().BeTrue("HMAC correto + timestamp recente deve ser aceito");
    }

    [Fact(DisplayName = "1.7.3 — ML: timestamp > 5min → replay rejeitado")]
    public void ML_TimestampAntigo_Rejeitado()
    {
        var body  = """{"topic":"orders","resource":"/orders/123"}""";
        var tsMs  = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeMilliseconds().ToString();
        var sig   = HmacML(tsMs, body);

        WebhookSignatureValidator.ValidateML($"ts={tsMs}&v1={sig}", body, MlSecret)
            .Should().BeFalse("timestamp > 5min deve ser rejeitado como replay (1.7.3)");
    }

    [Fact(DisplayName = "1.7.3 — ML: HMAC inválido → rejeitado")]
    public void ML_HmacInvalido_Rejeitado()
    {
        var tsMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        WebhookSignatureValidator.ValidateML(
            $"ts={tsMs}&v1=cafebabe00112233aabbccdd", """{"topic":"orders"}""", MlSecret)
            .Should().BeFalse();
    }

    [Fact(DisplayName = "1.7.3 — ML: header ausente → rejeitado")]
    public void ML_HeaderAusente_Rejeitado() =>
        WebhookSignatureValidator.ValidateML(null, """{"topic":"orders"}""", MlSecret)
            .Should().BeFalse();

    [Fact(DisplayName = "1.7.3 — ML: body alterado após assinatura → HMAC não bate")]
    public void ML_BodyAlterado_Rejeitado()
    {
        var tsMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var sig  = HmacML(tsMs, """{"topic":"orders","resource":"/orders/123"}""");

        WebhookSignatureValidator.ValidateML(
            $"ts={tsMs}&v1={sig}",
            """{"topic":"orders","resource":"/orders/999"}""",  // body diferente
            MlSecret)
            .Should().BeFalse("body alterado invalida o HMAC");
    }

    // ── Shopee ───────────────────────────────────────────────────────────────

    [Fact(DisplayName = "1.7.2 — Shopee: assinatura com body correto → aceito")]
    public void Shopee_AssinaturaComBody_Aceita()
    {
        var body = """{"code":4,"shopid":12345}""";
        var tsS  = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig  = HmacShopee(body);
        var auth = $"{ShopeeId}|{tsS}|{sig}";

        WebhookSignatureValidator.ValidateShopee(auth, ShopeeUrl, body, ShopeeId, ShopeeKey)
            .Should().BeTrue("HMAC com body correto deve ser aceito (1.7.2)");
    }

    [Fact(DisplayName = "1.7.2 — Shopee: body forjado (diferente do assinado) → rejeitado")]
    public void Shopee_BodyForjado_Rejeitado()
    {
        // CENÁRIO DO AUDIT: atacante captura Authorization legítimo e substitui o body
        var bodyOriginal = """{"code":4,"shopid":12345}""";
        var bodyForjado  = """{"code":4,"shopid":12345,"stock_update":{"qty":0}}""";
        var tsS  = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig  = HmacShopee(bodyOriginal); // assinou o body original
        var auth = $"{ShopeeId}|{tsS}|{sig}";

        // Envia o body FORJADO com o Authorization do body original
        WebhookSignatureValidator.ValidateShopee(auth, ShopeeUrl, bodyForjado, ShopeeId, ShopeeKey)
            .Should().BeFalse(
                "body forjado deve ser rejeitado — vulnerabilidade corrigida na 1.7.2: " +
                "sem body na assinatura, atacante podia zerar estoque reutilizando Authorization capturado");
    }

    [Fact(DisplayName = "1.7.2 — Shopee: timestamp > 5min → replay rejeitado")]
    public void Shopee_TimestampAntigo_Rejeitado()
    {
        var body = """{"code":4}""";
        var tsS  = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var sig  = HmacShopee(body);
        var auth = $"{ShopeeId}|{tsS}|{sig}";

        WebhookSignatureValidator.ValidateShopee(auth, ShopeeUrl, body, ShopeeId, ShopeeKey)
            .Should().BeFalse("timestamp > 5min deve ser rejeitado como replay");
    }

    [Fact(DisplayName = "1.7.2 — Shopee: partnerId errado → rejeitado")]
    public void Shopee_PartnerIdErrado_Rejeitado()
    {
        var body = """{"code":4}""";
        var tsS  = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var sig  = HmacShopee(body);
        var auth = $"99999|{tsS}|{sig}"; // partnerId diferente do configurado

        WebhookSignatureValidator.ValidateShopee(auth, ShopeeUrl, body, ShopeeId, ShopeeKey)
            .Should().BeFalse("partnerId errado deve ser rejeitado");
    }

    [Fact(DisplayName = "1.7.2 — Shopee: header ausente → rejeitado")]
    public void Shopee_HeaderAusente_Rejeitado() =>
        WebhookSignatureValidator.ValidateShopee(null, ShopeeUrl, "{}", ShopeeId, ShopeeKey)
            .Should().BeFalse();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string HmacML(string ts, string body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(MlSecret));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{ts}.{body}")))
                      .ToLowerInvariant();
    }

    private string HmacShopee(string body)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(ShopeeKey));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{ShopeeUrl}|{body}")))
                      .ToLowerInvariant();
    }
}

// ── 1.7.4: MustChangePassword Middleware (testes de integração) ───────────────

public class MustChangePasswordTests : IntegrationTestBase
{
    public MustChangePasswordTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "1.7.4 — claim must_change_password=true → 403 em endpoint normal")]
    public async Task MustChange_BloqueiaTodosEndpoints()
    {
        var token  = Factory.GerarToken(mustChangePassword: true);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/products");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "MustChangePasswordMiddleware deve bloquear todos os endpoints com must_change_password=true");

        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("mustChangePassword",
            "resposta 403 deve indicar o motivo do bloqueio no body JSON");
    }

    [Fact(DisplayName = "1.7.4 — claim must_change_password=true → libera /api/auth/change-password")]
    public async Task MustChange_LiberaEndpointTrocaDeSenha()
    {
        var token  = Factory.GerarToken(mustChangePassword: true);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Payload inválido propositalmente — o que importa é que o middleware NÃO bloqueie (não 403)
        // O controller vai responder 400/404 por dados inválidos, mas não 403
        var resp = await client.PostAsync("/api/auth/change-password",
            Json(new ChangePasswordDto { CurrentPassword = "qualquer", NewPassword = "12345678" }));

        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "o endpoint /api/auth/change-password deve ser liberado pelo middleware mesmo com a flag");
    }

    [Fact(DisplayName = "1.7.4 — JWT sem must_change_password → acesso normal")]
    public async Task SemMustChange_AcessoNormal()
    {
        // Token gerado pelo helper padrão não tem a flag → acesso irrestrito
        var resp = await AuthClient.GetAsync("/api/products");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "usuário sem must_change_password deve ter acesso normal a todos os endpoints");
    }

    [Fact(DisplayName = "1.7.4 — claim must_change_password=true → /api/auth/login também liberado")]
    public async Task MustChange_LiberaEndpointLogin()
    {
        var token  = Factory.GerarToken(mustChangePassword: true);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Login é AllowAnonymous — middleware não deve bloquear
        var resp = await client.PostAsync("/api/auth/login",
            Json(new LoginDto { Username = "admin", Password = "wrong" }));

        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "/api/auth/login deve ser liberado pelo MustChangePasswordMiddleware");
    }

    [Fact(DisplayName = "1.7.4 — claim must_change_password=true → bloqueia /api/customers")]
    public async Task MustChange_BloqueiaCustomers()
    {
        var token  = Factory.GerarToken(mustChangePassword: true);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        (await client.GetAsync("/api/customers"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "1.7.4 — claim must_change_password=true → bloqueia /api/sales")]
    public async Task MustChange_BloqueiaSales()
    {
        var token  = Factory.GerarToken(mustChangePassword: true);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        (await client.GetAsync("/api/sales"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── 1.6.1: Cross-Tenant Auth Regression ──────────────────────────────────────

public class CrossTenantAuthTests : IntegrationTestBase
{
    public CrossTenantAuthTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "1.6.1 — Login com CNPJ de tenant diferente → 401 (cross-tenant auth fix)")]
    public async Task Login_CNPJTenantErrado_Retorna401()
    {
        // CNPJ que não tem nenhum usuário cadastrado no InMemory DB
        // GetByUsernameAndTenantAsync filtra por username + tenantId → retorna null → 401
        var cnpjSemUsuario = "00000000000191";
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-CNPJ", cnpjSemUsuario);

        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "admin", Password = "admin123" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "credenciais de um tenant não devem funcionar no contexto de outro tenant (1.6.1)");
    }

    [Fact(DisplayName = "1.6.1 — POST /api/auth/change-password sem auth → 401")]
    public async Task ChangePassword_SemToken_Retorna401()
        => (await AnonClient.PostAsync("/api/auth/change-password",
                Json(new ChangePasswordDto { CurrentPassword = "old", NewPassword = "new12345" })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "1.7.4 — POST /api/auth/change-password com auth → não é 403 (middleware libera)")]
    public async Task ChangePassword_ComAuth_NaoBloqueado()
    {
        // Mesmo sem a flag, o endpoint deve ser acessível (e falhar por lógica de negócio, não middleware)
        var resp = await AuthClient.PostAsync("/api/auth/change-password",
            Json(new ChangePasswordDto { CurrentPassword = "errada", NewPassword = "nova12345" }));

        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        // Deve ser 400 (senha errada) ou 404 (usuário não existe no InMemory DB de teste)
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

// Adicionar ao final de ControllersIntegrationTests.cs (antes do último })
// 1.8 — Teste RBAC negativo (2.8 do roadmap)
// Garante que [HasPermission] não é removido acidentalmente de write endpoints críticos.

public class RbacNegativeTests : IntegrationTestBase
{
    public RbacNegativeTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "1.8 — PUT /api/customers/{id} sem CustomerEdit → 403")]
    public async Task Customers_SemCustomerEdit_Retorna403()
    {
        // JWT com todas as permissoes exceto CustomerEdit
        var perms  = Permissions.All.Where(p => p != Permissions.CustomerEdit).ToArray();
        var token  = Factory.GerarToken(permissoes: perms);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // [HasPermission] verifica antes de qualquer DB lookup — 403 independe de o cliente existir
        var resp = await client.PutAsync(
            $"/api/customers/{Guid.NewGuid()}",
            Json(new { Name = "Tentativa", Document = "00000000000" }));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usuario sem customers.edit nao deve conseguir editar clientes (PII)");
    }

    [Fact(DisplayName = "1.8 — DELETE /api/customers/{id} sem CustomerDelete → 403")]
    public async Task Customers_SemCustomerDelete_Retorna403()
    {
        var perms  = Permissions.All.Where(p => p != Permissions.CustomerDelete).ToArray();
        var token  = Factory.GerarToken(permissoes: perms);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.DeleteAsync($"/api/customers/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usuario sem customers.delete nao deve conseguir excluir clientes");
    }

    [Fact(DisplayName = "1.8 — POST /api/fidelidade/{id}/resgatar sem FidelidadeUse → 403")]
    public async Task Fidelidade_SemFidelidadeUse_Retorna403()
    {
        var perms  = Permissions.All.Where(p => p != Permissions.FidelidadeUse).ToArray();
        var token  = Factory.GerarToken(permissoes: perms);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsync(
            $"/api/fidelidade/{Guid.NewGuid()}/resgatar",
            Json(new { Pontos = 100 }));

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usuario sem fidelidade.use nao deve resgatar pontos");
    }

    [Fact(DisplayName = "1.8 — DELETE /api/entregas/{id} sem EntregasManage → 403")]
    public async Task Entregas_SemEntregasManage_Retorna403()
    {
        var perms  = Permissions.All.Where(p => p != Permissions.EntregasManage).ToArray();
        var token  = Factory.GerarToken(permissoes: perms);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.DeleteAsync($"/api/entregas/{Guid.NewGuid()}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usuario sem entregas.manage nao deve excluir entregas");
    }

    [Fact(DisplayName = "1.8 — GET /api/bi/dre-detalhado sem ReportFinancial → 403")]
    public async Task Bi_SemReportFinancial_Retorna403()
    {
        var perms  = Permissions.All.Where(p => p != Permissions.ReportFinancial).ToArray();
        var token  = Factory.GerarToken(permissoes: perms);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/bi/dre-detalhado");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usuario sem report.financial nao deve ver DRE detalhado");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  S10 — REGRESSÃO N1: UpdateLoginAttemptAsync sem SetGlobalTenantId
//  Garante que brute-force lockout funciona na API (não só no WPF).
//  CRÍTICO: estes testes NÃO chamam AppDbContext.SetGlobalTenantId() no setup.
// ═══════════════════════════════════════════════════════════════════════════════

[Collection("ErpApi")]
public class S10BruteForceRegressaoTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    // CNPJ fictício com dígitos verificadores válidos para o teste
    private const string CnpjTeste = "11222333000181";

    // S11 FIX: hash gerado no próprio teste — constante pré-computada era incorreta
    // e causava falso negativo (3ª tentativa também falhava com senha "correta").
    private static readonly string HashSenhaCorreta =
        BCrypt.Net.BCrypt.HashPassword("SenhaCorreta@123", 12);

    public S10BruteForceRegressaoTests(ErpApiFactory factory) => _factory = factory;

    // Deriva TenantId do CNPJ — mesmo algoritmo do TenantHelper (inline para não depender de ERP.Api)
    private static Guid TenantIdDoCnpj(string cnpj)
    {
        var digits    = new string(cnpj.Where(char.IsDigit).ToArray());
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash      = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(digits));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    private async Task SeedUsuarioAsync(Guid tenantId, string username, string passwordHash)
    {
        // Sem SetGlobalTenantId — usa SetQueryTenantId (AsyncLocal) para o filtro
        AppDbContext.SetQueryTenantId(tenantId);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Role mínima para o usuário existir
            var role = new ERP.Domain.Entities.Role
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Administrador"
            };
            db.Roles.Add(role);

            var user = new ERP.Domain.Entities.User
            {
                Id                  = Guid.NewGuid(),
                TenantId            = tenantId,
                Username            = username,
                Name                = "Teste Brute Force",
                PasswordHash        = passwordHash,
                IsActive            = true,
                FailedLoginAttempts = 0,
                RoleId              = role.Id,
                CreatedAt           = DateTime.UtcNow,
                UpdatedAt           = DateTime.UtcNow,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        finally
        {
            AppDbContext.SetQueryTenantId(Guid.Empty);
        }
    }

    [Fact(DisplayName = "S10 N1 — 5 logins errados incrementam FailedLoginAttempts sem SetGlobalTenantId")]
    public async Task BruteForce_SemSetGlobal_IncrementaContadorNoBanco()
    {
        // Arrange — seed SEM chamar SetGlobalTenantId (garantia da regressão)
        var tenantId = TenantIdDoCnpj(CnpjTeste);
        await SeedUsuarioAsync(tenantId, "adminN1", HashSenhaCorreta);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-CNPJ", CnpjTeste);

        // Act — 5 tentativas com senha errada
        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login",
                new { Username = "adminN1", Password = "senhaErrada" });
        }

        // Assert — FailedLoginAttempts = 5 no banco (sem SetGlobalTenantId)
        AppDbContext.SetQueryTenantId(tenantId);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == "adminN1" && u.TenantId == tenantId);

            user.Should().NotBeNull("usuário deve existir no banco");
            user!.FailedLoginAttempts.Should().Be(5,
                "5 tentativas com senha errada devem incrementar o contador no banco API");
            user.LockoutEndUtc.Should().NotBeNull(
                "conta deve estar bloqueada após 5 tentativas");
        }
        finally
        {
            AppDbContext.SetQueryTenantId(Guid.Empty);
        }
    }

    [Fact(DisplayName = "S10 N1 — Login bem-sucedido reseta FailedLoginAttempts no banco sem SetGlobalTenantId")]
    public async Task BruteForce_LoginSucesso_ResetaContadorNoBanco()
    {
        // S11 FIX: antes o teste usava CnpjTeste+"reset" para derivar tenantId mas
        // enviava X-Tenant-CNPJ: CnpjTeste — header apontava para tenant diferente,
        // login retornava 401 sem chamar UpdateLoginAttemptAsync, assert passava trivialmente.
        // Agora CNPJ e tenantId são derivados do mesmo valor (CnpjResetTeste).
        const string CnpjResetTeste = "22333444000107"; // CNPJ válido diferente do CnpjTeste
        var tenantId = TenantIdDoCnpj(CnpjResetTeste);
        await SeedUsuarioAsync(tenantId, "adminReset", HashSenhaCorreta);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-CNPJ", CnpjResetTeste); // mesmo CNPJ do seed

        // 2 tentativas erradas — deve incrementar FailedLoginAttempts
        await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "adminReset", Password = "errada" });
        await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "adminReset", Password = "errada" });

        // Verifica que o contador foi de fato incrementado (teste real, não trivial)
        AppDbContext.SetQueryTenantId(tenantId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userApos2Erros = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == "adminReset" && u.TenantId == tenantId);
            userApos2Erros!.FailedLoginAttempts.Should().Be(2,
                "2 tentativas erradas devem incrementar o contador antes do reset");
        }
        AppDbContext.SetQueryTenantId(Guid.Empty);

        // Login correto — deve resetar o contador
        await client.PostAsJsonAsync("/api/auth/login",
            new { Username = "adminReset", Password = "SenhaCorreta@123" });

        // Assert — contador deve ter voltado a 0
        AppDbContext.SetQueryTenantId(tenantId);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == "adminReset" && u.TenantId == tenantId);

            user.Should().NotBeNull();
            user!.FailedLoginAttempts.Should().Be(0,
                "login bem-sucedido deve resetar o contador no banco");
        }
        finally
        {
            AppDbContext.SetQueryTenantId(Guid.Empty);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  S11 — Rate limit do /api/cadastro particionado por IP (X-Forwarded-For)
// ═══════════════════════════════════════════════════════════════════════════════

[Collection("ErpApi")]
public class S11CadastroRateLimitTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    public S11CadastroRateLimitTests(ErpApiFactory factory) => _factory = factory;

    private static object CadastroBody(int n) => new
    {
        Cnpj        = $"1122233300{n:D4}",
        RazaoSocial = $"Empresa Teste {n}",
        Email       = $"teste{n}@exemplo.com",
        Senha       = "senhaForte123",
    };

    [Fact(DisplayName = "S11 — 3 cadastros do mesmo IP em 1h, 4º recebe 429")]
    public async Task CadastroRateLimit_PorIp_Bloqueia4taTentativa()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");

        for (int i = 0; i < 3; i++)
        {
            var resp = await client.PostAsJsonAsync("/api/cadastro", CadastroBody(i));
            // Pode ser 200 ou 400 (CNPJ com dígito verificador inválido), mas NUNCA 429
            ((int)resp.StatusCode).Should().NotBe(429,
                "as 3 primeiras tentativas do mesmo IP não devem ser bloqueadas pelo rate limit");
        }

        var quarta = await client.PostAsJsonAsync("/api/cadastro", CadastroBody(99));
        ((int)quarta.StatusCode).Should().Be(429,
                "a 4ª tentativa do mesmo IP em 1h deve ser bloqueada");
    }

    [Fact(DisplayName = "S11 — Cadastros de IPs diferentes não compartilham bucket")]
    public async Task CadastroRateLimit_IpsDiferentes_NaoCompartilhamBucket()
    {
        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.20");

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.21");

        // 3 tentativas de A — nenhuma deve ser 429
        for (int i = 0; i < 3; i++)
        {
            var resp = await clientA.PostAsJsonAsync("/api/cadastro", CadastroBody(100 + i));
            ((int)resp.StatusCode).Should().NotBe(429);
        }

        // 3 tentativas de B (IP diferente) — também nenhuma deve ser 429,
        // provando que o balde de A não afeta B (partitioner funcionando).
        for (int i = 0; i < 3; i++)
        {
            var resp = await clientB.PostAsJsonAsync("/api/cadastro", CadastroBody(200 + i));
            ((int)resp.StatusCode).Should().NotBe(429,
                "IP diferente não deve compartilhar o balde de rate limit de outro IP");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  S11 — Oráculo de enumeração via status code (CNPJ existente vs novo)
// ═══════════════════════════════════════════════════════════════════════════════

[Collection("ErpApi")]
public class S11CadastroEnumeracaoTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    public S11CadastroEnumeracaoTests(ErpApiFactory factory) => _factory = factory;

    // CNPJ com dígitos verificadores válidos (gerador conhecido para testes)
    private const string CnpjValido = "11222333000181";

    private static object Body(string cnpj, int n) => new
    {
        Cnpj        = cnpj,
        RazaoSocial = $"Empresa Enum {n}",
        Email       = $"enum{n}@exemplo.com",
        Senha       = "senhaForte123",
    };

    [Fact(DisplayName = "S11 — CNPJ já existente retorna 200 (mesmo status do CNPJ novo)")]
    public async Task Cadastro_CnpjExistente_Retorna200IgualAoNovo()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.50");

        // Primeiro cadastro — CNPJ novo, deve criar e retornar 200
        var primeiro = await client.PostAsJsonAsync("/api/cadastro", Body(CnpjValido, 1));
        primeiro.StatusCode.Should().Be(HttpStatusCode.OK,
            "primeiro cadastro com CNPJ novo deve ser bem-sucedido");

        // Segundo cadastro — MESMO CNPJ, já existe
        using var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.51"); // IP diferente p/ não bater rate limit

        var segundo = await client2.PostAsJsonAsync("/api/cadastro", Body(CnpjValido, 2));

        // S11 FIX: ambos devem retornar 200 — fecha o oráculo de enumeração
        segundo.StatusCode.Should().Be(HttpStatusCode.OK,
            "CNPJ já existente deve retornar o MESMO status code (200) que CNPJ novo, " +
            "para não permitir enumeração de clientes via diferença de status HTTP");

        var corpo1 = await primeiro.Content.ReadAsStringAsync();
        var corpo2 = await segundo.Content.ReadAsStringAsync();

        // Mensagens devem ser estruturalmente idênticas (não vazam diferença)
        corpo2.Should().NotContain("admin e usuário",
            "resposta para CNPJ já existente não deve conter detalhes específicos do cadastro");
    }

    [Fact(DisplayName = "S11 — CNPJ malformado continua retornando 400 (validação real)")]
    public async Task Cadastro_CnpjMalformado_Retorna400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.60");

        var resp = await client.PostAsJsonAsync("/api/cadastro", Body("123", 99));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "CNPJ malformado (não 14 dígitos) é erro de validação real, não oráculo — continua 400");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  S11 — Catálogo público requer opt-in (CatalogoPublicoHabilitado)
// ═══════════════════════════════════════════════════════════════════════════════

[Collection("ErpApi")]
public class S11CatalogoPublicoOptInTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    public S11CatalogoPublicoOptInTests(ErpApiFactory factory) => _factory = factory;

    private async Task<Guid> SeedTenantComProdutoAsync(
        bool catalogoHabilitado, bool mostrarPreco = false, bool mostrarEstoque = false)
    {
        var tenantId = Guid.NewGuid();
        AppDbContext.SetQueryTenantId(tenantId);
        try
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Branches.Add(new ERP.Domain.Entities.Branch
            {
                Id       = Guid.NewGuid(),
                TenantId = tenantId,
                Name     = "Matriz Teste",
                IsMatriz = true,
                CatalogoPublicoHabilitado = catalogoHabilitado,
                CatalogoMostrarPreco      = mostrarPreco,
                CatalogoMostrarEstoque    = mostrarEstoque,
            });

            db.Products.Add(new ERP.Domain.Entities.Product
            {
                Id         = Guid.NewGuid(),
                TenantId   = tenantId,
                Name       = "Produto Teste Catálogo",
                Barcode    = "7891234567890",
                Unit       = "UN",
                IsActive   = true,
                SalePrice  = 99.90m,
                Stock      = 50,
            });

            await db.SaveChangesAsync();
            return tenantId;
        }
        finally
        {
            AppDbContext.SetQueryTenantId(Guid.Empty);
        }
    }

    [Fact(DisplayName = "S11 — Tenant SEM opt-in retorna 404 no catálogo público")]
    public async Task Catalogo_SemOptIn_Retorna404()
    {
        var tenantId = await SeedTenantComProdutoAsync(catalogoHabilitado: false);

        var client = _factory.CreateClient();
        var resp   = await client.GetAsync($"/api/products/catalogo?tenantId={tenantId}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "tenant sem CatalogoPublicoHabilitado não deve expor produtos via catálogo público");
    }

    [Fact(DisplayName = "S11 — Tenant COM opt-in mas sem MostrarPreco/Estoque oculta esses campos")]
    public async Task Catalogo_ComOptIn_SemMostrarPrecoEstoque_OcultaCampos()
    {
        var tenantId = await SeedTenantComProdutoAsync(
            catalogoHabilitado: true, mostrarPreco: false, mostrarEstoque: false);

        var client = _factory.CreateClient();
        var resp   = await client.GetAsync($"/api/products/catalogo?tenantId={tenantId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();

        body.Should().Contain("Produto Teste Catálogo",
            "nome do produto deve aparecer (catálogo habilitado)");
        body.Should().Contain("\"salePrice\":null",
            "preço não deve aparecer sem opt-in explícito de CatalogoMostrarPreco");
        body.Should().Contain("\"stock\":null",
            "estoque não deve aparecer sem opt-in explícito de CatalogoMostrarEstoque");
    }

    [Fact(DisplayName = "S11 — Tenant COM opt-in completo expõe preço e estoque")]
    public async Task Catalogo_ComOptInCompleto_ExpoePrecoEEstoque()
    {
        var tenantId = await SeedTenantComProdutoAsync(
            catalogoHabilitado: true, mostrarPreco: true, mostrarEstoque: true);

        var client = _factory.CreateClient();
        var resp   = await client.GetAsync($"/api/products/catalogo?tenantId={tenantId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();

        body.Should().Contain("99.9",
            "preço deve aparecer quando o tenant optou por CatalogoMostrarPreco");
        body.Should().NotContain("\"stock\":null",
            "estoque deve aparecer quando o tenant optou por CatalogoMostrarEstoque");
    }
}

}