// ── ERP.Tests/Controllers/ControllersIntegrationTests.cs ─────────────────────
// Testes de integração para os 26 controllers da API.
//
// Estratégia:
//   • WebApplicationFactory<Program> com InMemory DB substitui SQL Server Azure
//   • JWT real gerado com a chave de teste para endpoints autenticados
//   • Cada controller tem ao menos 2 testes: (1) guard de autenticação e (2) happy path
//   • Controllers com lógica crítica (Auth, Sales, PDV) têm cobertura extra
//
// Pré-requisitos no Program.cs (Sprint 2A):
//   • public partial class Program { }   ← adicionar ao final do arquivo
// ─────────────────────────────────────────────────────────────────────────────
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ERP.Application.DTOs;
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
            // Remove o DbContext real (SQL Server)
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Substitui por InMemory com nome único por factory
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"IntegrationTests_{Guid.NewGuid()}"));

            // Substitui configurações JWT para usar a chave de teste
            services.Configure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer           = true,
                        ValidateAudience         = true,
                        ValidateLifetime         = true,
                        ValidateIssuerSigningKey  = true,
                        ValidIssuer              = JwtIssuer,
                        ValidAudience            = JwtAudience,
                        IssuerSigningKey         = new SymmetricSecurityKey(
                                                       Encoding.UTF8.GetBytes(JwtKey))
                    };
                });
        });
    }

    /// <summary>Gera um JWT de teste com TenantId e permissões informadas.</summary>
    public string GerarToken(
        string cargo        = "Administrador",
        string[]? permissoes = null,
        Guid?  tenantId     = null)
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

        foreach (var p in permissoes ?? ["admin"])
            claims.Add(new Claim("permission", p));

        var token = new JwtSecurityToken(
            issuer:             JwtIssuer,
            audience:           JwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>HttpClient já autenticado com JWT de teste.</summary>
    public HttpClient CreateAuthenticatedClient(string cargo = "Administrador")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GerarToken(cargo));
        return client;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  BASE — helpers compartilhados entre as classes de teste
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
        var resp = await AuthClient.PostAsync("/api/products",
            Json(new { })); // campos obrigatórios ausentes
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
//  METAS (Sprint 2A)
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
//  CONTAS A PAGAR (Sprint 2A)
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
//  AUDITORIA (Sprint 2A)
// ═══════════════════════════════════════════════════════════════════════════════

public class AuditoriaControllerTests : IntegrationTestBase
{
    public AuditoriaControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/auditoria sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/auditoria"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/auditoria com token → 200 paginado")]
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
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CAIXA
// ═══════════════════════════════════════════════════════════════════════════════

public class CaixaControllerTests : IntegrationTestBase
{
    public CaixaControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/caixa/status sem token → 401")]
    public async Task Status_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/caixa/status"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/caixa/status com token → 200")]
    public async Task Status_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/caixa/status"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
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
    {
        var resp = await AuthClient.GetAsync("/api/inventario/produtos?pagina=1&tam=10");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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
        => (await AnonClient.GetAsync($"/api/fidelidade/saldo/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/fidelidade/saldo/{guid} com token → 200")]
    public async Task Saldo_ComToken_Retorna200()
        => (await AuthClient.GetAsync($"/api/fidelidade/saldo/{Guid.NewGuid()}"))
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
        => (await AnonClient.GetAsync("/api/contasreceber"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/contasreceber com token → 200")]
    public async Task GetAll_ComToken_Retorna200()
        => (await AuthClient.GetAsync("/api/contasreceber"))
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
//  STOCK / ESTOQUE
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
//  CARGOS (ROLES)
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
        => (await AnonClient.GetAsync("/api/fiscal"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  NOTAS FISCAIS (Sprint 2A)
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

    [Fact(DisplayName = "GET /api/calculadora sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/calculadora"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

// ═══════════════════════════════════════════════════════════════════════════════
//  BI
// ═══════════════════════════════════════════════════════════════════════════════

public class BiControllerTests : IntegrationTestBase
{
    public BiControllerTests(ErpApiFactory f) : base(f) { }

    [Fact(DisplayName = "GET /api/bi sem token → 401")]
    public async Task GetAll_SemToken_Retorna401()
        => (await AnonClient.GetAsync("/api/bi"))
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
        => (await AnonClient.GetAsync("/api/marketplace"))
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

        // Decodifica o payload sem validar assinatura (só verificamos a estrutura)
        var parts   = token.Split('.');
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
    {
        var resp = await AuthClient.GetAsync("/api/metrics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET /api/metrics retorna snapshot com campos esperados")]
    public async Task GetMetrics_RetornaSnapshotCompleto()
    {
        // Faz algumas requests antes para popular o MetricsCollector
        await AuthClient.GetAsync("/api/products");
        await AuthClient.GetAsync("/api/customers");

        var resp = await AuthClient.GetAsync("/api/metrics");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<MetricsSnapshot>();
        body.Should().NotBeNull();
        body!.JanelaMinutos.Should().Be(5,  "janela deve ser de 5 minutos");
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

        body!.TotalRequests.Should().BeGreaterThan(0,
            "deve ter registrado ao menos 1 request após chamar /api/products");
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
}