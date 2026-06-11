using ERP.Api.Hubs;
using ERP.Api.Middleware;
using ERP.Api.Services;
using ERP.Application.Interfaces;
using ERP.Application.Mappings;
using ERP.Application.Services;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Repositories;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.UnitOfWork;
using ERP.Persistence.Context;
using AspNetCoreRateLimit;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Context;
using System.IO.Compression;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── S2.1+S2.2: Serilog com Enrichment e JSON estruturado ─────────────────────
// Enrich.FromLogContext() é o que faz LogContext.PushProperty() funcionar.
// WithProperty("App") marca cada log com o nome da aplicação — útil em ambientes
// com múltiplos serviços no mesmo Seq/Grafana.
// O formato JSON já estava configurado no appsettings.json (CompactJsonFormatter).
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()                          // S2.1: habilita LogContext.PushProperty
       .Enrich.WithProperty("App",         "ConstruTTor.Api")
       .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName));

// ── Banco de dados ────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// ── Fase 0 Fix: DbContext com IRequestTenant Scoped por requisição ────────────
// NÃO usar AddDbContext puro aqui — ele criaria AppDbContext com o construtor
// de 1-argumento (WPF), sem IRequestTenant. Usamos uma factory explícita que
// injeta IRequestTenant (já registrado como Scoped abaixo) no construtor da API.
// Resultado: cada requisição HTTP tem seu próprio AppDbContext isolado por tenant.
builder.Services.AddSingleton(
    new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(connectionString)
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        .Options);

builder.Services.AddScoped<AppDbContext>(sp => new AppDbContext(
    sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
    sp.GetRequiredService<IRequestTenant>()
));

// ── AutoMapper ────────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(typeof(MappingProfile));

// ── Repositórios e UoW ────────────────────────────────────────────────────────
builder.Services.AddScoped<IProductRepository,  ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISaleRepository,     SaleRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IUserRepository,     UserRepository>();
builder.Services.AddScoped<IRoleRepository,     RoleRepository>();
builder.Services.AddScoped<IBrandRepository,    BrandRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IUnitOfWork,         UnitOfWork>();

// ── Serviços de aplicação ─────────────────────────────────────────────────────
builder.Services.AddScoped<IProductService,      ProductService>();
builder.Services.AddScoped<ICustomerService,     CustomerService>();
builder.Services.AddScoped<ISaleService,         SaleService>();
builder.Services.AddScoped<IAuthService,         AuthService>();
builder.Services.AddScoped<IContaReceberService, ContaReceberService>();
builder.Services.AddScoped<IInventarioService,    InventarioService>();
builder.Services.AddScoped<IDreService,           DreService>();
builder.Services.AddScoped<IAbcService,           AbcService>();
builder.Services.AddScoped<IMargemService,        MargemService>();
builder.Services.AddScoped<IFluxoCaixaService,    FluxoCaixaService>();
builder.Services.AddScoped<IPedidoCompraService,  ERP.Application.Services.PedidoCompraService>();
builder.Services.AddScoped<IOrcamentoService,      ERP.Infrastructure.Services.OrcamentoService>();
builder.Services.AddScoped<ICaixaService,          ERP.Application.Services.CaixaService>();
builder.Services.AddScoped<IDevolucaoService,      ERP.Application.Services.DevolucaoService>();
builder.Services.AddScoped<IRoleService,           ERP.Application.Services.RoleService>();
builder.Services.AddScoped<ICalculadoraService, CalculadoraService>();

// ── Asaas (boleto bancário) ───────────────────────────────────────────────────
builder.Services.AddHttpClient<ERP.Infrastructure.Services.AsaasService>();

// Fase 0 Fix: HaverService agora injeta AppDbContext e IRequestTenant diretamente
// (sem IServiceProvider e sem CreateScope — que criava scope novo com tenant vazio).
builder.Services.AddScoped<IHaverService, ERP.Infrastructure.Services.HaverService>();

builder.Services.AddScoped<IProdutoAgregadoService, ERP.Infrastructure.Services.ProdutoAgregadoService>();
builder.Services.AddScoped<ISugestaoComprasService, ERP.Infrastructure.Services.SugestaoComprasService>();

builder.Services.AddScoped<IEntregaService, ERP.Infrastructure.Services.EntregaService>();
builder.Services.AddScoped<ERP.Application.Interfaces.INfceEmissionService,
                            ERP.Application.Services.NfceEmissionService>();
builder.Services.AddScoped<ERP.Application.Interfaces.INfeCancellationService,
                            ERP.Application.Services.NfeCancellationService>();

builder.Services.AddScoped<IContaPagarService,
                            ERP.Infrastructure.Services.ContaPagarService>();
builder.Services.AddScoped<ERP.Application.Interfaces.IMetasService,
                            ERP.Infrastructure.Services.MetasService>();
builder.Services.AddScoped<ERP.Application.Interfaces.INotasFiscaisService,
                            ERP.Infrastructure.Services.NotasFiscaisService>();
builder.Services.AddScoped<ERP.Application.Interfaces.IAuditLogService,
                            ERP.Infrastructure.Services.AuditLogService>();
builder.Services.AddScoped<ERP.Infrastructure.Services.IStorageService, ERP.Infrastructure.Services.StorageService>();

// S2.6 — AuditSqlService: trilha de auditoria para ExecuteSqlRawAsync
builder.Services.AddScoped<ERP.Infrastructure.Services.AuditSqlService>();

builder.Services.AddMemoryCache();

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.Configure<IpRateLimitOptions>(opt =>
{
    opt.EnableEndpointRateLimiting = true;
    opt.StackBlockedRequests       = false;
    opt.RealIpHeader               = "X-Real-IP";
    opt.ClientIdHeader             = "X-ClientId";
    opt.GeneralRules = new List<RateLimitRule>
    {
        new() { Endpoint = "POST:/api/Auth/login", Period = "1m", Limit = 10  },
        new() { Endpoint = "*",                    Period = "1m", Limit = 300  },
        new() { Endpoint = "*",                    Period = "1h", Limit = 3000 }
    };
});
builder.Services.AddSingleton<IIpPolicyStore,        MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy,    AsyncKeyLockProcessingStrategy>();

// ── Fase 2 ────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ERP.Infrastructure.Services.ITransferenciaService,
                            ERP.Infrastructure.Services.TransferenciaService>();
builder.Services.AddScoped<ERP.Application.Interfaces.INfseEmissionService,
                            ERP.Infrastructure.Services.NfseEmissionService>();
builder.Services.AddSingleton<ERP.Domain.Services.Fiscal.ICMSSTCalculator>();
builder.Services.AddSingleton<ERP.Infrastructure.Services.OfflineSyncService>();
builder.Services.AddSingleton<ERP.Infrastructure.Services.SpedEfdGenerator>();
builder.Services.AddSingleton<ERP.Infrastructure.Services.SpedContribuicoesGenerator>();
builder.Services.AddScoped<ERP.Infrastructure.Services.ContaReceberService>();

// ── Fase 3 ────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ERP.Application.Interfaces.IBIService,
                            ERP.Infrastructure.Services.BIService>();
builder.Services.AddHttpClient<ERP.Infrastructure.Services.MarketplaceService>();
builder.Services.AddScoped<ERP.Infrastructure.Services.MarketplaceService>();

builder.Services.AddHttpClient<IFocusNfeHttpClient, ERP.Infrastructure.HttpClients.FocusNfeHttpClient>(client =>
{
    client.BaseAddress = new Uri("https://api.focusnfe.com.br/v2/");
});
builder.Services.AddSingleton<ERP.Infrastructure.Services.TEFService>();

// ── FluentValidation ──────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<ERP.Application.Validators.CreateProductValidator>();

// ── S2.4: Application Insights ───────────────────────────────────────────────
// ConnectionString configurada em appsettings.json → ApplicationInsights:ConnectionString
// ou via Azure App Service → Configuration (recomendado para produção).
// Captura automaticamente: requests, dependências (SQL, HTTP), exceptions, traces.
builder.Services.AddApplicationInsightsTelemetry(opts =>
{
    opts.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    opts.EnableAdaptiveSampling  = false; // desativa amostragem — queremos 100% dos dados
    opts.EnableRequestTrackingTelemetryModule = true;
});

// F1.3 — Enriquece CADA evento do App Insights com TenantId, UserId e CorrelationId.
// Sem isso é impossível filtrar erros/latência por loja no portal Azure.
builder.Services.AddSingleton<Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer,
    ERP.Api.Services.TenantTelemetryInitializer>();

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey      = builder.Configuration["Jwt:Key"]!;
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew                = TimeSpan.Zero,
            // Mapeia o claim "role_name" do JWT para o sistema de roles do ASP.NET Core.
            // Sem isso [Authorize(Roles = "Administrador")] sempre retorna 403.
            RoleClaimType            = "role_name"
        };

        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path  = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) &&
                    (path.StartsWithSegments("/hubs/erp") ||
                     path.StartsWithSegments("/hubs/erp-chat")))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Registra uma policy ASP.NET Core por código de permissão.
    // Cada policy exige que o JWT contenha um claim "permission" com o valor correspondente.
    // [HasPermission(Permissions.XYZ)] resolve para [Authorize(Policy = "xyz")] via HasPermissionAttribute.
    foreach (var perm in ERP.Api.Security.Permissions.All)
        options.AddPolicy(perm, policy => policy.RequireClaim("permission", perm));
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var corsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddPolicy("ApiPolicy", policy =>
    {
        var origins = corsOrigins.Length > 0
            ? corsOrigins
            : new[] { "https://red-rock-0d2fb150f7.azurestaticapps.net" };

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    }));

// ── Response Compression ──────────────────────────────────────────────────────
builder.Services.AddResponseCompression(opt =>
{
    opt.EnableForHttps = true;
    opt.Providers.Add<BrotliCompressionProvider>();
    opt.Providers.Add<GzipCompressionProvider>();
    opt.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/json; charset=utf-8",
        "text/plain",
        "text/html"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opt => opt.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opt =>  opt.Level = CompressionLevel.Fastest);

// ── SignalR com backplane ─────────────────────────────────────────────────────
var azureSignalRConn = builder.Configuration["AzureSignalRConnectionString"];
if (!string.IsNullOrWhiteSpace(azureSignalRConn))
    builder.Services.AddSignalR().AddAzureSignalR(azureSignalRConn);
else
    builder.Services.AddSignalR();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sql-azure",
        tags: ["database", "ready"]);

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestTenant, RequestTenant>();
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddScoped<NotificacaoService>();

// S2.7: MetricsCollector singleton — vive durante toda a vida da aplicação
builder.Services.AddSingleton<MetricsCollector>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "TTSoft ERP API",
        Version     = "v1",
        Description = "API pública do TTSoft ERP — produtos, clientes, vendas e estoque."
    });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Informe o token JWT: Bearer {seu_token}"
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddScoped<IFidelidadeService, FidelidadeService>();

var app = builder.Build();

// ── S1.3: Exception handler seguro ───────────────────────────────────────────
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    Log.Error(ex, "Erro HTTP não tratado em {Path} [CorrelationId: {CorrelationId}]",
        ctx.Request.Path, ctx.TraceIdentifier);
    ctx.Response.StatusCode  = 500;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new
    {
        erro          = "Erro interno do servidor. Contate o suporte.",
        correlationId = ctx.TraceIdentifier
    });
}));

app.UseResponseCompression();

// S2.3: CorrelationId PRIMEIRO — para que todos os middlewares seguintes já
// tenham o ID disponível no LogContext e no TraceIdentifier
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseIpRateLimiting();

// S2.7: Metrics ANTES do request logging — para capturar latência real
app.UseMiddleware<MetricsMiddleware>();

app.UseSerilogRequestLogging(opts =>
{
    // S2.2: enriquece os logs de request com propriedades extras
    opts.EnrichDiagnosticContext = (diag, httpContext) =>
    {
        diag.Set("RequestHost",   httpContext.Request.Host.Value);
        diag.Set("RequestScheme", httpContext.Request.Scheme);
        diag.Set("UserAgent",     httpContext.Request.Headers.UserAgent.ToString());
    };
});

app.UseSwagger();
app.UseSwaggerUI(opt =>
{
    opt.SwaggerEndpoint("/swagger/v1/swagger.json", "TTSoft ERP API v1");
    opt.RoutePrefix = string.Empty;
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("ApiPolicy");
app.UseAuthentication();
app.UseAuthorization();

// TenantMiddleware APÓS autenticação — para ter claims disponíveis
// S2.1: já enriquece o LogContext com TenantId e UserId
app.UseMiddleware<TenantMiddleware>();

// F1.2 — Rate limiting por tenant_id (complementa o UseIpRateLimiting por IP acima).
// Posição: APÓS TenantMiddleware (que popula IRequestTenant com o tenant do JWT).
// Limites: 5 req/min/tenant no login, 200 req/min/tenant nas demais rotas.
app.UseMiddleware<TenantRateLimitMiddleware>();

// S2.5: Brute force APÓS rate limiting (que já bloqueia) — só para alertar
app.UseMiddleware<BruteForceAlertMiddleware>();

app.UseMiddleware<ETagMiddleware>();

app.MapControllers();
app.MapHub<ERPHub>("/hubs/erp");
app.MapHub<ERPChatHub>("/hubs/erp-chat");

// ── Health endpoints ──────────────────────────────────────────────────────────
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status    = report.Status.ToString(),
            database  = report.Entries.TryGetValue("sql-azure", out var dbEntry)
                            ? dbEntry.Status.ToString() : "unknown",
            timestamp = DateTime.UtcNow,
            version   = "1.0.0",
            uptime    = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess()
                            .StartTime.ToUniversalTime()).ToString(@"dd\.hh\:mm\:ss")
        });
        await ctx.Response.WriteAsync(result);
    }
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

app.Run();

public partial class Program { }