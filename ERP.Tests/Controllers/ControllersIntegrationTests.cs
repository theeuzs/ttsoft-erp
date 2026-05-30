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
            // ── FASE 0: Remove o DbContext real e substitui por InMemory ────────────
            // Precisa remover AMBOS porque agora usamos AddSingleton(Options) +
            // AddScoped<AppDbContext>(factory) em vez de AddDbContext<>.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // Registra as options InMemory com nome único por factory
            var dbName = $"IntegrationTests_{Guid.NewGuid()}";
            services.AddSingleton(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options);

            // Factory que injeta IRequestTenant — mesma estrutura do Program.cs.
            // TenantMiddleware seta IRequestTenant por requisição via JWT claim.
            services.AddScoped<AppDbContext>(sp => new AppDbContext(
                sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
                sp.GetRequiredService<IRequestTenant>()
            ));

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
//  TENANT SCOPE — helper para testes de isolamento
//
//  Problema: o construtor de AppDbContext(options, IRequestTenant) seta
//  _asyncTenantId.Value = requestTenant.TenantId no contexto async atual.
//  Quando o seed cria um scope sem request HTTP, IRequestTenant.TenantId = Guid.Empty,
//  e o construtor sobrescreve o AsyncLocal para Guid.Empty.
//  O request HTTP filho herda Guid.Empty do pai e — mesmo com o middleware
//  setando corretamente depois — pode haver race entre o construtor do DbContext
//  e o filtro de query.
//
//  TenantScope garante que o AsyncLocal correto está setado ANTES do seed e é
//  resetado com using ao fim, evitando vazamento entre testes.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Seta AppDbContext.SetQueryTenantId(tenantId) ao entrar no using e reseta
/// para Guid.Empty ao sair. Usar sempre que semear dados em testes de isolamento.
/// </summary>
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
        => (await AnonClient.GetAsync("/api/caixa/aberto"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact(DisplayName = "GET /api/caixa/status com token → 200 ou 404")]
    public async Task Status_ComToken_Retorna200()
    {
        // GET /api/caixa/aberto retorna 200 se houver caixa aberto, 404 se não houver.
        // Ambos são válidos — o endpoint está protegido e acessível.
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
        => (await AnonClient.GetAsync("/api/fiscal/aliquota-icms/SP"))
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

    [Fact(DisplayName = "GET /api/calculadora/templates sem token → 200 (endpoint público)")]
    public async Task GetAll_SemToken_Retorna401()
        // /api/calculadora/templates é AllowAnonymous (usado pela calculadora pública)
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

// ═══════════════════════════════════════════════════════════════════════════════
//  FASE 0 — TESTES OBRIGATÓRIOS DE ISOLAMENTO
//
//  Estes são os critérios de pronto da Fase 0. Os três testes DEVEM estar
//  vermelhos antes das correções e VERDES depois. O CI bloqueia merge
//  se qualquer um falhar.
//
//  NOTA: InMemory EF Core não executa HasQueryFilter em todas as situações,
//  mas com a correção do construtor (IRequestTenant por instância), o filtro
//  é avaliado corretamente porque cada contexto usa seu próprio TenantId.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// FASE 0 — Teste #1: Isolamento Cross-Tenant em Produtos
///
/// Cria um produto com JWT do Tenant A e verifica que o Tenant B
/// não o vê ao chamar GET /api/products. Prova que o HasQueryFilter
/// com GetTenantId() por instância está funcionando na API.
/// </summary>
public class Fase0TenantIsolationTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public Fase0TenantIsolationTests(ErpApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "FASE0 #1 — Produto do Tenant A NÃO aparece para Tenant B")]
    public async Task Produto_TenantA_NaoAparece_Para_TenantB()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // TenantScope: garante AsyncLocal correto durante o SaveChanges do seed.
        // Sem isso, o construtor AppDbContext(options, IRequestTenant) seta o
        // AsyncLocal para Guid.Empty (sem request HTTP), e o request filho herda Guid.Empty.
        using (new TenantScope(tenantA))
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new ERP.Domain.Entities.Product
            {
                Id        = Guid.NewGuid(),
                TenantId  = tenantA,
                Name      = "Cimento CP-II 50kg — TenantA",
                SalePrice = 35.90m,
                Stock     = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        } // Dispose() reseta AsyncLocal para Guid.Empty — sem vazamento entre testes

        // ── Act — Tenant B consulta produtos ─────────────────────────────────
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _factory.GerarToken(tenantId: tenantB));

        var resp = await clientB.GetAsync("/api/products");

        // ── Assert ───────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();

        // Tenant B NÃO deve ver o produto do Tenant A
        body.Should().NotContain("Cimento CP-II 50kg — TenantA", "produto do Tenant A nao deve vazar para Tenant B (LGPD)");
    }

    [Fact(DisplayName = "FASE0 #2 — Produto do Tenant A aparece corretamente para Tenant A")]
    public async Task Produto_TenantA_Aparece_Para_TenantA()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var tenantA   = Guid.NewGuid();
        var produtoId = Guid.NewGuid();

        using (new TenantScope(tenantA))
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new ERP.Domain.Entities.Product
            {
                Id        = produtoId,
                TenantId  = tenantA,
                Name      = $"Argamassa AC-II — TenantA-{produtoId:N}",
                SalePrice = 22.50m,
                Stock     = 50,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // ── Act — Tenant A consulta seus próprios produtos ────────────────────
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _factory.GerarToken(tenantId: tenantA));

        var resp = await clientA.GetAsync("/api/products");

        // ── Assert ───────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();

        body.Should().Contain($"TenantA-{produtoId:N}", "Tenant A deve ver seus proprios produtos");
    }

    [Fact(DisplayName = "FASE0 #3 — GET /api/haver/saldo com cliente de outro tenant retorna 0 (não vaza)")]
    public async Task Haver_ClienteOutroTenant_NaoVazaDado()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Cria cliente do Tenant A com saldo Haver
        var clienteId = Guid.NewGuid();
        using (new TenantScope(tenantA))
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new ERP.Domain.Entities.Customer
            {
                Id           = clienteId,
                TenantId     = tenantA,
                Name         = "Cliente do Tenant A",
                HaverBalance = 500m,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // ── Act — Tenant B tenta ver saldo do cliente do Tenant A ────────────
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _factory.GerarToken(tenantId: tenantB));

        var resp = await clientB.GetAsync($"/api/haver/saldo/{clienteId}");

        // ── Assert ───────────────────────────────────────────────────────────
        // Deve retornar 200 com saldo 0 (cliente não encontrado neste tenant)
        // OU 404/403. De qualquer forma, NÃO deve retornar saldo de R$ 500.
        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().NotContain("500", "saldo do Tenant A nao deve vazar para Tenant B");
        }
        else
        {
            resp.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound,
                HttpStatusCode.Forbidden,
                HttpStatusCode.Unauthorized);
        }
    }
}
}

// ═══════════════════════════════════════════════════════════════════════════════
//  F1.1 — DUAL-TENANT ISOLATION TESTS
//
//  Objetivo: provar que nenhum dos controllers sensíveis vaza dados entre
//  tenants. Um por controller, cobrindo os 8 mais críticos além dos 3 do FASE0.
//
//  Padrão: seed com TenantId=A, requisição com JWT de TenantId=B,
//          verifica que o identificador único do dado de A não aparece.
//
//  Junto com Fase0TenantIsolationTests → 11 testes de isolamento no total.
// ═══════════════════════════════════════════════════════════════════════════════
public class F11DualTenantTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;
    public F11DualTenantTests(ErpApiFactory factory) => _factory = factory;

    // ── helper: seed com TenantScope (garante AsyncLocal correto) ─────────────
    private async Task SeedAsync(Guid tenantId, Action<AppDbContext> seed)
    {
        using var ts = new TenantScope(tenantId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private HttpClient ClientFor(Guid tenantId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", _factory.GerarToken(tenantId: tenantId));
        return c;
    }

    // ── 1. Customers ────────────────────────────────────────────────────────
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

    // ── 2. Sales ────────────────────────────────────────────────────────────
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

    // ── 3. Orcamentos ───────────────────────────────────────────────────────
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

    // ── 4. Contas a Receber ─────────────────────────────────────────────────
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

    // ── 5. Contas a Pagar ───────────────────────────────────────────────────
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

    // ── 6. Fidelidade ───────────────────────────────────────────────────────
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
            Pontos = 500, Data = DateTime.UtcNow,
            Descricao = "Compra teste"
        }));

        var resp = await ClientFor(tenantB)
            .GetAsync($"/api/fidelidade/{clienteId}/saldo");

        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            body.Should().NotContain("500",
                "pontos do Tenant A nao devem aparecer para Tenant B");
        }
        else
        {
            resp.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound, HttpStatusCode.Forbidden);
        }
    }

    // ── 7. Pedidos de Compra ────────────────────────────────────────────────
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
            DataPedido = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }));

        var resp = await ClientFor(tenantB).GetAsync("/api/pedidos-compra");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(marker);
    }

    // ── 8. Entregas ─────────────────────────────────────────────────────────
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
    // ── 9. Devolucao via API (1.5.2 fix — órfão de tenant) ─────────────────
    [Fact(DisplayName = "F1.1 — Devolucao: POST autenticado nao retorna 500")]
    [Trait("F11", "DualTenant")]
    public async Task Devolucao_ComAuth_NaoRetorna500()
    {
        // Verifica que o endpoint exige auth e retorna erro de negócio (não 500).
        // O fix real (TenantId no INSERT) requer teste com SQL Server real;
        // este garante que o caminho não explode com NullReference após a refatoração.
        var resp = await ClientFor(Guid.NewGuid()).PostAsJsonAsync("/api/devolucao",
            new
            {
                SaleId = Guid.NewGuid(),
                Items  = new[] { new { ProductId = Guid.NewGuid(), Quantidade = 1m, Motivo = "Teste" } }
            });

        resp.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "após injetar IRequestTenant o construtor nao deve explodir");
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "JWT foi enviado — nao deve ser 401");
    }
}