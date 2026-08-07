// ERP.Tests/WPF/FinalizarVendaFallbackOfflineTests.cs
using ERP.Application.DTOs;
using ERP.Application.Exceptions;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Infrastructure.Services;
using ERP.WPF.ViewModels;
using FluentAssertions;
using Moq;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// Fase 2 do Offline-First — testa TentarCriarVendaComFallbackOfflineAsync
/// isolado (achado da revisão cruzada com GPT/Gemini: testar
/// FinalizarVendaAsync inteiro exigiria um MessageBox.Show real aparecendo
/// durante dotnet test, travando esperando clique humano — não roda em CI).
/// Usa o OfflineSyncService REAL com SQLite temporário (não mockado —
/// SalvarVendaOfflineComOutboxAsync não é virtual, e verificar o dado
/// realmente persistido é mais fiel do que só checar se um mock foi
/// chamado — é exatamente o que o GPT pediu: confirmar que Sale.Id e
/// SalePaymentId gravados são os mesmos gerados no cliente, não novos.
/// </summary>
public class FinalizarVendaFallbackOfflineTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly OfflineSyncService _offlineDb;
    private readonly Mock<ISaleService> _saleServiceMock;

    public FinalizarVendaFallbackOfflineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"offline_fallback_test_{Guid.NewGuid()}.db");
        _offlineDb = new OfflineSyncService(_tempDbPath);
        _saleServiceMock = new Mock<ISaleService>();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
    }

    private static CreateSaleDto DtoDeTeste(Guid vendaId, Guid salePaymentId) => new()
    {
        Id = vendaId,
        UsuarioId = Guid.NewGuid(),
        Items = new() { new CreateSaleItemDto { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 100m } },
        Payments = new() { new CreateSalePaymentDto { Id = salePaymentId, PaymentMethod = PaymentMethod.Dinheiro, Amount = 100m } }
    };

    [Fact]
    public async Task FalhaDeConectividade_SalvaOfflineComOMesmoSaleIdEMesmoSalePaymentId()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var dto = DtoDeTeste(vendaId, salePaymentId);

        // Embrulhado do MESMO jeito que SaleService.CreateAsync realmente
        // embrulha (catch(Exception) dentro da transação) — não a exceção "crua".
        _saleServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new Exception("ERRO NA VENDA (revertido): timeout", new TimeoutException()));

        var (venda, ficouOffline) = await FinalizarVendaViewModel.TentarCriarVendaComFallbackOfflineAsync(
            _saleServiceMock.Object, _offlineDb, dto, "Consumidor", "Vendedor", 100m);

        ficouOffline.Should().BeTrue();
        venda.Id.Should().Be(vendaId, "o fallback nunca pode gerar um Sale.Id novo — perderia a idempotência");

        // Confirma o dado REALMENTE persistido no SQLite, não só o retorno do método.
        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().HaveCount(1);

        using var doc = JsonDocument.Parse(pendentes[0].Json);
        doc.RootElement.GetProperty("Id").GetGuid().Should().Be(vendaId);
        var pagamentoSalvo = doc.RootElement.GetProperty("Payments")[0];
        pagamentoSalvo.GetProperty("Id").GetGuid().Should().Be(salePaymentId,
            "o SalePaymentId também precisa ser exatamente o gerado no cliente, senão a idempotência financeira quebra na sincronização");
    }

    [Fact]
    public async Task ErroDeNegocio_NuncaSalvaOffline_ExcecaoPropagaPraOOperador()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var dto = DtoDeTeste(vendaId, salePaymentId);

        var causaReal = new LimiteCreditoExcedidoException("Cliente Teste", 500m, 400m, 200m);
        _saleServiceMock.Setup(s => s.CreateAsync(dto))
            .ThrowsAsync(new Exception("ERRO NA VENDA (revertido): limite excedido", causaReal));

        Func<Task> acao = () => FinalizarVendaViewModel.TentarCriarVendaComFallbackOfflineAsync(
            _saleServiceMock.Object, _offlineDb, dto, "Consumidor", "Vendedor", 100m);

        // A exceção tem que ESCAPAR do método — quem chama (FinalizarVendaAsync)
        // deixa isso estourar pro operador ver, exatamente como já era antes
        // da Fase 2 existir.
        await acao.Should().ThrowAsync<Exception>()
            .WithMessage("*limite excedido*");

        // E o mais importante: nada foi pra offline por engano.
        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().BeEmpty("erro de negócio nunca pode virar venda offline silenciosamente");
    }

    [Fact]
    public async Task Sucesso_NaoTocaNoOfflineDeJeitoNenhum()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var dto = DtoDeTeste(vendaId, salePaymentId);
        var vendaEsperada = new SaleDto(vendaId, "PDV-001", "Consumidor", "Vendedor", DateTime.Now, SaleStatus.SemNota, "Dinheiro", 100m);

        _saleServiceMock.Setup(s => s.CreateAsync(dto)).ReturnsAsync(vendaEsperada);

        var (venda, ficouOffline) = await FinalizarVendaViewModel.TentarCriarVendaComFallbackOfflineAsync(
            _saleServiceMock.Object, _offlineDb, dto, "Consumidor", "Vendedor", 100m);

        ficouOffline.Should().BeFalse();
        venda.Should().BeEquivalentTo(vendaEsperada);

        var pendentes = await _offlineDb.GetEventosPendentesAsync();
        pendentes.Should().BeEmpty();
    }
}
