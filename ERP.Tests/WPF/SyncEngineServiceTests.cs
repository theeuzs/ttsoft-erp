// ERP.Tests/WPF/SyncEngineServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.Exceptions;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Infrastructure.Services;
using ERP.WPF.Services;
using FluentAssertions;
using Moq;
using System;
using System.IO;
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

        _engine = new SyncEngineService(
            _offlineDb, _saleServiceMock.Object,
            _productServiceMock.Object, _customerServiceMock.Object,
            _motorFinanceiroMock.Object);
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
}
