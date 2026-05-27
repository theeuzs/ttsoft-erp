using ERP.Application.Interfaces;
using ERP.Application.Mappings;
using ERP.Application.Services;
using ERP.Application.Validators;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Repositories;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.UnitOfWork;
using ERP.Persistence.Context;
using ERP.WPF.Helpers;
using ERP.WPF.Services;
using ERP.WPF.ViewModels;
using ERP.WPF.Views;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Serilog;
using Serilog.Formatting.Compact;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ERP.Domain.Services.Fiscal;
using ERP.Infrastructure.HttpClients;
using System.Threading.Tasks;

namespace ERP.WPF;

public partial class App : System.Windows.Application
{
    public static IConfiguration Configuration { get; private set; } = null!;
    public static IServiceProvider Services { get; private set; } = null!;

    static App()
    {
        var culture = new System.Globalization.CultureInfo("pt-BR");
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    public App()
    {
        string pastaLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(pastaLog, "log-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        Log.Information("=== Sistema iniciado ===");

        EventManager.RegisterClassHandler(typeof(Window), Window.PreviewKeyDownEvent, new KeyEventHandler(GlobalPreviewKeyDown));
    }

    private void GlobalPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && sender is Window window)
        {
            if (window.GetType() != typeof(MainWindow))
                window.Close();
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── HANDLERS GLOBAIS DE ERRO ──────────────────────────────────
        // Handler 1: Erros em Tasks não observadas (async void, fire-and-forget)
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Log.Error(args.Exception, "Task não observada lançou exceção (async void ou fire-and-forget).");
            args.SetObserved(); // Impede o crash do processo
        };

        // Handler 2: Erros fatais em qualquer thread (último recurso antes do crash)
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "Exceção fatal não tratada em thread secundária. IsTerminating={IsTerminating}", args.IsTerminating);
            
            if (args.IsTerminating)
            {
                Log.CloseAndFlush(); // Garante que o log é salvo antes de morrer
                MessageBox.Show(
                    $"Erro crítico no sistema.\n\n{ex?.Message}\n\nUm log foi gerado em Logs/. Contate o suporte.",
                    "Erro Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        // ── FIM DOS HANDLERS GLOBAIS ──────────────────────────────────

        try
        {
            var culture = new System.Globalization.CultureInfo("pt-BR");
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            // ─── Passo 1: Verificar se é a primeira execução ───────────────
            if (!SecureConfigService.Existe())
            {
                this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var setup = new SetupView();
                bool? configurado = setup.ShowDialog();

                if (configurado != true)
                {
                    Shutdown();
                    return;
                }
            }

            // ─── Passo 2: Carregar a connection string criptografada ────────
            string connectionString;
            try
            {
                connectionString = SecureConfigService.Carregar();
                if (!connectionString.Contains("MultipleActiveResultSets"))
                    connectionString += ";MultipleActiveResultSets=True";
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Falha ao carregar configuração de conexão.");
                MessageBox.Show(
                    "Não foi possível ler a configuração do banco de dados.\n\n" +
                    "O arquivo 'conexao.dat' pode estar corrompido.\n" +
                    "Exclua-o e reinicie o sistema para reconfigurar.",
                    "Erro de Configuração", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // ─── Passo 3: Carregar appsettings.json ────────────────────────
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            Configuration = builder.Build();

            // ─── Passo 3.5: Aplicar tema salvo ────────────────────────────
            var tema = ThemeService.Carregar();
            ThemeService.Aplicar(tema);
            ThemeService.GarantirCardShadow();

            // ─── Passo 4: SETAR O TENANTID GLOBAL ─────────────────────────
            var tenantId = ERP.WPF.Services.TenantService.GetCurrentTenantId();
            ERP.Persistence.Context.AppDbContext.SetGlobalTenantId(tenantId);
            Log.Information("TenantId ativo: {TenantId}", tenantId);

            // ─── Passo 5: Montar o container de injeção de dependência ──────
            var services = new ServiceCollection();
            ConfigureServices(services, connectionString);
            Services = services.BuildServiceProvider();

            // ─── Passo 6: Seed do banco ────────────────────────────────────
            using (var scope = Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DbSeeder.Seed(context);
            }

            // ─── Passo 7: Verificação de Atualização Automática ────────────
            var atualizacao = await ERP.WPF.Services.UpdateService.VerificarAtualizacaoAsync();

            if (atualizacao != null)
            {
                var updateView = new Views.UpdateView(atualizacao);
                bool? resultado = updateView.ShowDialog();

                if (resultado == true)
                    return;

                if (atualizacao.Obrigatoria)
                {
                    MessageBox.Show(
                        "Esta atualização é obrigatória. O sistema será fechado.",
                        "Atualização Obrigatória", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }
            }

            // ─── Passo 8: Tela de Login ────────────────────────────────────
            var loginVm = Services.GetRequiredService<LoginViewModel>();
            var loginView = new LoginView(loginVm);
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool? loginResult = loginView.ShowDialog();

            if (loginResult == true)
            {
                var mainWindow = Services.GetRequiredService<MainWindow>();
                this.ShutdownMode = ShutdownMode.OnLastWindowClose;
                mainWindow.Show();

                var worker = Services.GetRequiredService<NfeContingencyWorker>();
                _ = worker.IniciarTrabalhoEmBackgroundAsync(() =>
                {
                    var config = ConfiguracaoService.Carregar();
                    return (config.TokenFocusNfe, config.UsarAmbienteProducao);
                });
            }
            else
            {
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Erro crítico durante a inicialização do sistema.");
            MessageBox.Show($"Erro ao iniciar o sistema:\n\n{ex.Message}",
                "Falha Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void Application_DispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Loga com stack trace completo
        Log.Fatal(e.Exception, 
            "Erro fatal na UI thread. Tela: {Source}, Tipo: {ExType}", 
            e.Exception.Source ?? "Desconhecido",
            e.Exception.GetType().Name);

        // Loga inner exceptions (muitas vezes o erro real está aqui)
        var inner = e.Exception.InnerException;
        while (inner != null)
        {
            Log.Fatal(inner, "  └─ InnerException: {Msg}", inner.Message);
            inner = inner.InnerException;
        }

        // Garante que o log é gravado no disco AGORA (não espera o buffer)
        Log.CloseAndFlush();

        // Mostra mensagem amigável para o operador
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}\n\n" +
            "Um log de diagnóstico foi gerado na pasta Logs.\n" +
            "O sistema continuará funcionando.",
            "Erro no Sistema", MessageBoxButton.OK, MessageBoxImage.Error);

        // Marca como tratado para o app NÃO fechar
        e.Handled = true;

        // Reinicializa o Serilog (porque fizemos CloseAndFlush acima)
        string pastaLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                formatter: new CompactJsonFormatter(),
                path: Path.Combine(pastaLog, "log-.json"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== Sistema encerrado ===");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConfiguration>(Configuration);

        // ── Database ──────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options
                .UseSqlServer(connectionString,
                    b => b.MigrationsAssembly("ERP.Persistence"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // ── AutoMapper ────────────────────────────────────────────────────
        services.AddAutoMapper(typeof(MappingProfile));

        // ── FluentValidation ──────────────────────────────────────────────
        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

        // ── Http Clients & Polly ──────────────────────────────────────────
        services.AddHttpClient<IFocusNfeHttpClient, FocusNfeHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.focusnfe.com.br/v2/");
        })
        .AddTransientHttpErrorPolicy(p =>
            p.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(3, attempt - 1))))
        .AddTransientHttpErrorPolicy(p =>
            p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

        // ── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();

        // ── UnitOfWork ────────────────────────────────────────────────────
        services.AddScoped<IBrandRepository,    BrandRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Usuário atual (injetável, testável — substitui AppSession estático) ──
        services.AddScoped<ICurrentUser, CurrentUserService>();

        // ── Cache (IMemoryCache) ──────────────────────────────────────────
        services.AddMemoryCache();

        // ── Application Services ──────────────────────────────────────────
        services.AddScoped<IProductService,   ProductService>();
        services.AddScoped<ISupplierService,  SupplierService>();
        services.AddScoped<ICategoryService,  CategoryService>();
        services.AddScoped<IBrandService,     BrandService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IAuditLogService,  AuditLogService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDevolucaoService, DevolucaoService>(); 
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ICaixaService, CaixaService>();
        services.AddScoped<IOrcamentoService, OrcamentoService>();
        services.AddScoped<IContaPagarService, ContaPagarService>();
        services.AddTransient<IContaReceberService, ContaReceberService>(sp =>
            new ContaReceberService(
                sp.GetRequiredService<IUnitOfWork>(),
                sp));
        services.AddScoped<INfeImportService, NfeImportService>();
        services.AddScoped<ISpedService, SpedService>();
        services.AddScoped<INfceEmissionService, NfceEmissionService>();
        services.AddScoped<INfeEmissionService, NfeEmissionService>();
        services.AddScoped<INfeCancellationService, NfeCancellationService>();
        services.AddScoped<INfeStatusService, NfeStatusService>();
        services.AddScoped<INfeContingencyService, NfeContingencyService>();
        services.AddSingleton<NfeContingencyWorker>();
        services.AddScoped<ILegacyImportService, LegacyImportService>();
        services.AddScoped<IFiscalCalculator, SimplesNacionalCalculator>();
        services.AddScoped<IMotorFiscalService, MotorFiscalService>();
        // Produtos Agregados / Auto-Sugestão no PDV
        services.AddScoped<IProdutoAgregadoService, ProdutoAgregadoService>();

        services.AddScoped<IPedidoCompraService, ERP.Application.Services.PedidoCompraService>();

        // ── Serviços de relatório com cache ─────────────────────────
        services.AddScoped<DreService>();
        services.AddScoped<IDreService>(sp =>
            new DreServiceCached(
                sp.GetRequiredService<DreService>(),
                sp.GetRequiredService<IMemoryCache>()));

        services.AddScoped<AbcService>();
        services.AddScoped<IAbcService>(sp =>
            new AbcServiceCached(
                sp.GetRequiredService<AbcService>(),
                sp.GetRequiredService<IMemoryCache>()));

        services.AddScoped<ComissaoService>();
        services.AddScoped<IComissaoService>(sp =>
            new ComissaoServiceCached(
                sp.GetRequiredService<ComissaoService>(),
                sp.GetRequiredService<IMemoryCache>()));

        services.AddScoped<MargemService>();
        services.AddScoped<IMargemService>(sp =>
            new MargemServiceCached(
                sp.GetRequiredService<MargemService>(),
                sp.GetRequiredService<IMemoryCache>()));

        services.AddScoped<FluxoCaixaService>();
        services.AddScoped<IFluxoCaixaService>(sp =>
            new FluxoCaixaServiceCached(
                sp.GetRequiredService<FluxoCaixaService>(),
                sp.GetRequiredService<IMemoryCache>()));

        // Fase 0 Fix: WpfRequestTenant lê TenantId/UserId dos estáticos do AppDbContext
        // (preenchidos no login via SetGlobalTenantId + SetCurrentUser).
        // HaverService e FidelidadeService agora injetam IRequestTenant diretamente
        // em vez de usar IServiceProvider/CreateScope (que criava scope sem tenant).
        services.AddScoped<IRequestTenant, ERP.WPF.Services.WpfRequestTenant>();
        services.AddScoped<IHaverService, HaverService>();
        services.AddScoped<ERP.Application.Interfaces.IFidelidadeService,
                   ERP.Infrastructure.Services.FidelidadeService>();

        // ── Fase 2: Novos serviços ────────────────────────────────────────
        services.AddScoped<ERP.Infrastructure.Services.ITransferenciaService,
                           ERP.Infrastructure.Services.TransferenciaService>();
        services.AddScoped<ERP.Application.Interfaces.INfseEmissionService,
                           ERP.Infrastructure.Services.NfseEmissionService>();
        services.AddSingleton<ERP.Domain.Services.Fiscal.ICMSSTCalculator>();
        services.AddSingleton<ERP.Infrastructure.Services.OfflineSyncService>();
        services.AddSingleton<ERP.Infrastructure.Services.SpedEfdGenerator>();

        // ── Fase 3 ────────────────────────────────────────────────────────
        services.AddScoped<ERP.Application.Interfaces.IBIService,
                           ERP.Infrastructure.Services.BIService>();
        services.AddSingleton<ERP.Infrastructure.Services.TEFService>();
        services.AddSingleton<ERP.Infrastructure.Services.BalancaService>();
        services.AddSingleton<ERP.Infrastructure.Services.SpedContribuicoesGenerator>();

        services.AddScoped<InventarioService>();
        services.AddScoped<IInventarioService>(sp =>
            new InventarioServiceCached(
                sp.GetRequiredService<InventarioService>(),
                sp.GetRequiredService<IMemoryCache>()));

        // ── ViewModels ────────────────────────────────────────────────────
        services.AddTransient<ProductViewModel>();
        services.AddSingleton<PdvViewModel>(); // Singleton: preserva carrinho e vendas suspensas ao trocar de tela
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SaleViewModel>();
        services.AddTransient<CustomerViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<UserViewModel>();
        services.AddTransient<ConfiguracoesViewModel>();
        services.AddTransient<CargosViewModel>();
        services.AddTransient<FilialViewModel>();
        services.AddTransient<BIViewModel>();
        services.AddTransient<CatalogoViewModel>();
        services.AddSingleton<ERP.WPF.Services.ChatService>(_ =>
            new ERP.WPF.Services.ChatService(
                "https://erp-ttsoft-api-g8bde4f6aqcwb9aw.brazilsouth-01.azurewebsites.net"));
        services.AddTransient<OrcamentosViewModel>();
        services.AddTransient<FinanceiroViewModel>();
        services.AddTransient<ContaPagarViewModel>();
        services.AddTransient<NfeImportViewModel>();
        services.AddTransient<SpedViewModel>();
        services.AddTransient<NotasFiscaisViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<AjusteEstoqueViewModel>();
        services.AddTransient<ResumoCaixaViewModel>();
        services.AddTransient<ComprasViewModel>();

        // ── Windows ───────────────────────────────────────────────────────
        services.AddTransient<MainWindow>();
        services.AddTransient<LoginView>();
        services.AddTransient<ConfiguracoesView>();
        services.AddTransient<FinanceiroView>();
        services.AddTransient<ContaPagarView>();
    }
}