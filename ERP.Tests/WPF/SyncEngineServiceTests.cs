// ERP.Tests/WPF/SyncEngineServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.Exceptions;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Infrastructure.Services;
using ERP.WPF.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// Fase B da migração WPF→API (08/2026) — testa o comportamento combinado
/// com o GPT pra SessaoExpiradaException dentro de ProcessarOutboxAsync:
/// sessão expirada é um problema GLOBAL de autenticação, não um erro da
/// venda específica que estava sendo processada — o ciclo inteiro precisa
/// parar, sem marcar nada como sincronizado, sem descartar nada, e sem
/// incrementar o contador de tentativas da venda que só falhou por causa
/// de um token inválido, não por culpa própria.
///
/// Usa o mesmo padrão comprovado em FinalizarVendaFallbackOfflineTests:
/// OfflineSyncService REAL com SQLite temporário (não mockado), só
/// ISaleService/IMotorFinanceiroService/etc mockados via Moq.
/// </summary>
public class SyncEngineServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly OfflineSyncService _offlineDb;
    private readonly Mock<ISaleService> _saleServiceMock;
    private readonly Mock<IProductService> _productServiceMock;
    private readonly Mock<ICustomerService> _customerServiceMock;
    private readonly Mock<IMotorFinanceiroService> _motorFinanceiroMock;
    private readonly SyncEngineService _engine;

    public SyncEngineServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"sync_engine_test_{Guid.NewGuid()}.db");
        _offlineDb = new OfflineSyncService(_tempDbPath);
        _saleServiceMock = new Mock<ISaleService>();
        _productServiceMock = new Mock<IProductService>();
        _customerServiceMock = new Mock<ICustomerService>();
        _motorFinanceiroMock = new Mock<IMotorFinanceiroService>();

        // Achado (15/09) — SyncEngineService passou a abrir um escopo de DI
        // próprio dentro de cada chamada de sincronização (corrige DbContext
        // compartilhado entre as duas tarefas paralelas). Pro teste continuar
        // valendo, o escopo fake precisa devolver os MESMOS mocks que o
        // teste já usa e verifica — senão a chamada real bateria num
        // ServiceProvider vazio, não nos mocks configurados aqui.
        var scopedProvider = new Mock<IServiceProvider>();
        scopedProvider.Setup(p => p.GetService(typeof(IProductService))).Returns(_productServiceMock.Object);
        scopedProvider.Setup(p => p.GetService(typeof(ICustomerService))).Returns(_customerServiceMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(scopedProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _engine = new SyncEngineService(
            _offlineDb, _saleServiceMock.Object,
            _productServiceMock.Object, _customerServiceMock.Object,
            _motorFinanceiroMock.Object, scopeFactory.Object);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
    }

    private static CreateSaleDto DtoDeTeste(Guid vendaId) => new()
    {
        Id = vendaId,
        UsuarioId = Guid.NewGuid(),
        Items = { new CreateSaleItemDto { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100m } },
        Payments = { new CreateSalePaymentDto { Id = Guid.NewGuid(), PaymentMethod = PaymentMethod.Dinheiro, Amount = 100m } }
    };

    private async Task SemearTresEventosPendentesAsync(Guid vendaA, Guid vendaB, Guid vendaC)
    {
        // Insere na ordem A → B → C — GetEventosPendentesAsync ordena por
        // CriadoEm, então a ordem de processamento é previsível pro teste.
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaA, DtoDeTeste(vendaA));
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaB, DtoDeTeste(vendaB));
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaC, DtoDeTeste(vendaC));
    }

    [Fact(DisplayName = "SessaoExpiradaException na 1ª venda — interrompe o ciclo, não tenta a 2ª nem a 3ª")]
    public async Task SessaoExpirada_InterrompeOCiclo_NaoTentaOsEventosSeguintes()
    {
        var (vendaA, vendaB, vendaC) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await SemearTresEventosPendentesAsync(vendaA, vendaB, vendaC);

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ThrowsAsync(new SessaoExpiradaException());

        var sucessos = await _engine.ProcessarOutboxAsync();

        sucessos.Should().Be(0);
        // A prova central: CreateAsync só foi chamado UMA vez — se o ciclo
        // não tivesse parado, teria sido chamado 3 vezes (uma por venda).
        _saleServiceMock.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Once,
            "sessão expirada é problema global — não faz sentido bater com o mesmo token inválido nas outras vendas da fila");
    }

    [Fact(DisplayName = "SessaoExpiradaException — nenhuma venda é marcada como sincronizada nem descartada")]
    public async Task SessaoExpirada_NaoMarcaNadaComoSincronizado_TudoContinuaPendente()
    {
        var (vendaA, vendaB, vendaC) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await SemearTresEventosPendentesAsync(vendaA, vendaB, vendaC);

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ThrowsAsync(new SessaoExpiradaException());

        await _engine.ProcessarOutboxAsync();

        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().HaveCount(3, "nenhuma venda pode ser marcada como sincronizada nem descartada — todas continuam na fila pro próximo ciclo tentar de novo");
    }

    [Fact(DisplayName = "SessaoExpiradaException — não incrementa o contador de tentativas da venda afetada")]
    public async Task SessaoExpirada_NaoIncrementaTentativasDaVendaQueFalhou()
    {
        var vendaA = Guid.NewGuid();
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaA, DtoDeTeste(vendaA));

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ThrowsAsync(new SessaoExpiradaException());

        await _engine.ProcessarOutboxAsync();

        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().ContainSingle().Which.Tentativas.Should().Be(0,
            "não é justo contar isso como uma tentativa fracassada DESSA venda — o problema é autenticação global, não dela");
    }

    [Fact(DisplayName = "SessaoExpiradaException — nunca chama o Motor Financeiro pra essa venda")]
    public async Task SessaoExpirada_NuncaChamaMotorFinanceiro()
    {
        var vendaA = Guid.NewGuid();
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaA, DtoDeTeste(vendaA));

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ThrowsAsync(new SessaoExpiradaException());

        await _engine.ProcessarOutboxAsync();

        _motorFinanceiroMock.Verify(
            m => m.ProcessarRecebimentoVendaAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<System.Collections.Generic.IEnumerable<(Guid, PaymentMethod, decimal)>>()),
            Times.Never);
    }

    [Fact(DisplayName = "Erro de negócio comum (não sessão expirada) continua com o comportamento antigo — segue pras próximas vendas")]
    public async Task ErroComum_NaoInterrompeOCiclo_ContinuaProximaVenda()
    {
        // Regressão — confirma que só SessaoExpiradaException ganhou o
        // tratamento novo; qualquer outro erro continua best-effort por item.
        var (vendaA, vendaB) = (Guid.NewGuid(), Guid.NewGuid());
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaA, DtoDeTeste(vendaA));
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaB, DtoDeTeste(vendaB));

        _saleServiceMock.SetupSequence(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ThrowsAsync(new InvalidOperationException("produto não existe mais"))
            .ReturnsAsync(new SaleDto(vendaB, "PDV-002", "Cliente", "Vendedor", DateTime.Now, SaleStatus.SemNota, "Dinheiro", 100m));

        await _engine.ProcessarOutboxAsync();

        _saleServiceMock.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Exactly(2),
            "erro de negócio comum não pode travar as outras vendas da fila — só SessaoExpiradaException faz isso");
    }

    // ── Caminho feliz — não existia teste nenhum pra isso (motor "crítico" com 0% do fluxo normal coberto) ──

    [Fact(DisplayName = "Sincronização bem-sucedida: chama Motor Financeiro com os dados certos e marca como sincronizada")]
    public async Task ProcessarOutbox_Sucesso_ChamaMotorFinanceiroEMarcaSincronizada()
    {
        var vendaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var dto = DtoDeTeste(vendaId);
        dto.CustomerId = clienteId;
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, dto);

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "PDV-001", "Cliente", "Vendedor", DateTime.Now, SaleStatus.SemNota, "Dinheiro", 100m));
        _customerServiceMock.Setup(c => c.GetByIdAsync(clienteId))
            .ReturnsAsync(new CustomerDto(Id: clienteId, Document: "12345678900", Name: "Cliente Teste", Phone: null, City: null, HaverBalance: 0m));

        var sucessos = await _engine.ProcessarOutboxAsync();

        sucessos.Should().Be(1);
        _motorFinanceiroMock.Verify(m => m.ProcessarRecebimentoVendaAsync(
            vendaId, dto.UsuarioId, clienteId, "Cliente Teste", It.IsAny<string>(), "Sync Automático",
            dto.Troco, It.IsAny<System.Collections.Generic.IEnumerable<(Guid, PaymentMethod, decimal)>>()),
            Times.Once);

        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().BeEmpty("depois de sincronizar com sucesso, o evento não pode continuar pendente");
    }

    [Fact(DisplayName = "Cliente sem CustomerId — usa \"Consumidor Final\", não quebra por causa disso")]
    public async Task ProcessarOutbox_SemCliente_UsaConsumidorFinal()
    {
        var vendaId = Guid.NewGuid();
        var dto = DtoDeTeste(vendaId); // CustomerId nulo, o default do DtoDeTeste
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, dto);

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "PDV-001", null, "Vendedor", DateTime.Now, SaleStatus.SemNota, "Dinheiro", 100m));

        await _engine.ProcessarOutboxAsync();

        _motorFinanceiroMock.Verify(m => m.ProcessarRecebimentoVendaAsync(
            vendaId, dto.UsuarioId, null, "Consumidor Final", It.IsAny<string>(), "Sync Automático",
            dto.Troco, It.IsAny<System.Collections.Generic.IEnumerable<(Guid, PaymentMethod, decimal)>>()),
            Times.Once);
        _customerServiceMock.Verify(c => c.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName = "CRÍTICO: pagamento sem Id lança exceção antes de chamar o Motor Financeiro (idempotência granular quebraria)")]
    public async Task ProcessarOutbox_PagamentoSemId_NaoChamaMotorFinanceiro()
    {
        var vendaId = Guid.NewGuid();
        var dto = DtoDeTeste(vendaId);
        dto.Payments[0].Id = null; // simula payload antigo/malformado sem a chave de idempotência

        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, dto);
        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "PDV-001", null, "Vendedor", DateTime.Now, SaleStatus.SemNota, "Dinheiro", 100m));

        await _engine.ProcessarOutboxAsync();

        _motorFinanceiroMock.Verify(m => m.ProcessarRecebimentoVendaAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<decimal>(),
            It.IsAny<System.Collections.Generic.IEnumerable<(Guid, PaymentMethod, decimal)>>()),
            Times.Never, "sem Id em cada pagamento, ProcessarRecebimentoVendaAsync não tem como ser idempotente — melhor falhar alto");
    }

    [Fact(DisplayName = "Erro de negócio comum registra a falha no evento certo (achado real: JSON usa \"Id\" PascalCase, não \"id\")")]
    public async Task ProcessarOutbox_ErroComum_RegistraFalhaComEntidadeIdCorreta()
    {
        var vendaId = Guid.NewGuid();
        await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, DtoDeTeste(vendaId));

        _saleServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ThrowsAsync(new InvalidOperationException("produto não existe mais"));

        await _engine.ProcessarOutboxAsync();

        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().ContainSingle().Which.Tentativas.Should().Be(1,
            "ExtrairEntidadeId precisa achar o Id (PascalCase) no JSON pra RegistrarFalhaEventoAsync rodar de verdade");
    }

    // ── SincronizarCatalogoAsync — nenhum teste existia pra esse método inteiro ──

    [Fact(DisplayName = "Sincronizar catálogo: produtos e clientes com sucesso — retorna true")]
    public async Task SincronizarCatalogo_TudoComSucesso_RetornaTrue()
    {
        _productServiceMock.Setup(p => p.GetAllAsync()).ReturnsAsync(new List<ProductDto>());
        _customerServiceMock.Setup(c => c.GetAllAsync()).ReturnsAsync(new List<CustomerDto>());

        var resultado = await _engine.SincronizarCatalogoAsync();

        resultado.Should().BeTrue();
    }

    [Fact(DisplayName = "Sincronizar catálogo: falha em produtos — retorna false")]
    public async Task SincronizarCatalogo_FalhaEmProdutos_RetornaFalse()
    {
        _productServiceMock.Setup(p => p.GetAllAsync()).ThrowsAsync(new HttpRequestException("offline"));
        _customerServiceMock.Setup(c => c.GetAllAsync()).ReturnsAsync(new List<CustomerDto>());

        var resultado = await _engine.SincronizarCatalogoAsync();

        resultado.Should().BeFalse();
    }

    [Fact(DisplayName = "CRÍTICO (achado de auditoria, 24/08): falha só em clientes também tem que dar false — antes só olhava produtos, indicador de conectividade mentia \"online\" com catálogo de clientes desatualizado")]
    public async Task SincronizarCatalogo_FalhaEmClientes_RetornaFalse()
    {
        _productServiceMock.Setup(p => p.GetAllAsync()).ReturnsAsync(new List<ProductDto>());
        _customerServiceMock.Setup(c => c.GetAllAsync()).ThrowsAsync(new HttpRequestException("offline"));

        var resultado = await _engine.SincronizarCatalogoAsync();

        resultado.Should().BeFalse("produtos sincronizar sozinho não é suficiente — clientes desatualizado também é catálogo desatualizado");
    }

    [Fact(DisplayName = "Sincronizar catálogo: produtos e clientes rodam em paralelo, não em sequência (os dois GetAllAsync são chamados mesmo se um demorar)")]
    public async Task SincronizarCatalogo_ProdutosEClientes_RodamEmParalelo()
    {
        var produtosChamado = new TaskCompletionSource<bool>();
        var clientesChamado = new TaskCompletionSource<bool>();

        _productServiceMock.Setup(p => p.GetAllAsync()).Returns(async () =>
        {
            produtosChamado.TrySetResult(true);
            await clientesChamado.Task.WaitAsync(TimeSpan.FromSeconds(2));
            return new List<ProductDto>();
        });
        _customerServiceMock.Setup(c => c.GetAllAsync()).Returns(async () =>
        {
            clientesChamado.TrySetResult(true);
            await produtosChamado.Task.WaitAsync(TimeSpan.FromSeconds(2));
            return new List<CustomerDto>();
        });

        // Se rodassem em sequência, um dos dois nunca teria seu sinal disparado
        // a tempo do outro — isso trava em deadlock/timeout se não for paralelo.
        var resultado = await _engine.SincronizarCatalogoAsync().WaitAsync(TimeSpan.FromSeconds(3));

        resultado.Should().BeTrue();
    }
}