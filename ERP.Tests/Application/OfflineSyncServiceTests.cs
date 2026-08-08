using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using ERP.WPF.Services;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace ERP.Tests.Application.Services
{
    /// <summary>
    /// Fase 1 do Offline-First — testes de idempotência, atomicidade e da
    /// fila de sincronização, pedidos explicitamente antes de conectar o
    /// PDV (Fase 2). Ver docs/OFFLINE_FIRST_ARCHITECTURE.md.
    /// </summary>
    public class OfflineSyncServiceTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly OfflineSyncService _offlineDb;

        public OfflineSyncServiceTests()
        {
            // Banco SQLite temporário e isolado por teste — nunca toca no
            // arquivo real de LocalApplicationData do desenvolvedor.
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"offline_test_{Guid.NewGuid()}.db");
            _offlineDb = new OfflineSyncService(_tempDbPath);
        }

        public void Dispose()
        {
            // Microsoft.Data.Sqlite mantém um pool de conexões vivo por
            // processo mesmo depois de cada "using var conn" fechar — é assim
            // que ele reaproveita conexões rápido em produção. Só que isso
            // segura o handle do arquivo aberto no SO, e apagar o arquivo de
            // teste logo em seguida falha com "em uso por outro processo".
            // Isso é comportamento normal do driver, não bug de produção — o
            // app real nunca tenta apagar o banco enquanto está rodando.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        }

        private static CreateSaleDto DtoDeTeste(Guid id) => new()
        {
            Id = id,
            UsuarioId = Guid.NewGuid(),
            Items = new List<CreateSaleItemDto>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m }
            },
            Payments = new List<CreateSalePaymentDto>
            {
                new() { Id = Guid.NewGuid(), PaymentMethod = ERP.Domain.Enums.PaymentMethod.Dinheiro, Amount = 10m }
            }
        };

        // ── 2. Atomicidade da Outbox (§16.5) ────────────────────────────────

        [Fact(DisplayName = "Offline: venda e evento da Outbox são gravados juntos, na mesma transação")]
        public async Task SalvarVendaOfflineComOutbox_GravaVendaEEventoJuntos()
        {
            var vendaId = Guid.NewGuid();
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, DtoDeTeste(vendaId));

            var pendentes = await _offlineDb.GetEventosPendentesAsync();
            pendentes.Should().HaveCount(1);
            pendentes[0].Id.Should().NotBeNullOrEmpty();

            var status = await _offlineDb.GetStatusAsync();
            status.VendasPendentes.Should().Be(1);
        }

        [Fact(DisplayName = "Offline: se a venda já existe (mesmo Id), a segunda gravação falha sem deixar Outbox órfã")]
        public async Task SalvarVendaOfflineComOutbox_IdDuplicado_FalhaSemGravarParcial()
        {
            // ── 5. Duplicidade na Outbox — o próprio Id como chave primária
            // já impede duas linhas pra mesma venda; o teste confirma que a
            // segunda tentativa lança (constraint) e NÃO deixa um evento
            // órfão na Outbox pra essa venda.
            var vendaId = Guid.NewGuid();
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, DtoDeTeste(vendaId));

            Func<Task> segundaGravacao = () => _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, DtoDeTeste(vendaId));
            await segundaGravacao.Should().ThrowAsync<Exception>();

            // Continua só 1 evento pendente pra essa venda — a tentativa que
            // falhou não deixou um segundo evento na Outbox por trás.
            var pendentes = await _offlineDb.GetEventosPendentesAsync();
            pendentes.Should().HaveCount(1);
        }

        // ── 3. Retry — falha registra tentativa/erro e não trava a fila ────

        [Fact(DisplayName = "Offline: falha ao sincronizar incrementa Tentativas e grava UltimoErro, sem travar outros eventos")]
        public async Task RegistrarFalhaEvento_IncrementaTentativasEGravaErro()
        {
            var vendaOk    = Guid.NewGuid();
            var vendaFalha = Guid.NewGuid();
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaOk,    DtoDeTeste(vendaOk));
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaFalha, DtoDeTeste(vendaFalha));

            var pendentesAntes = await _offlineDb.GetEventosPendentesAsync();
            var eventoFalha = pendentesAntes.First(p => p.Json.Contains(vendaFalha.ToString()));

            await _offlineDb.RegistrarFalhaEventoAsync(eventoFalha.Id, vendaFalha, "Produto inexistente");

            // O evento que falhou continua pendente (pra tentar de novo no
            // próximo ciclo) — só ganhou tentativa/erro registrados.
            var pendentesDepois = await _offlineDb.GetEventosPendentesAsync();
            pendentesDepois.Should().HaveCount(2, "a falha não deve remover o evento da fila, só marcar a tentativa");

            var status = await _offlineDb.GetStatusAsync();
            status.VendasComErro.Should().Be(1);
            status.VendasPendentes.Should().Be(2, "o evento que deu certo (vendaOk) ainda não foi marcado como sincronizado nesse teste");
        }

        // ── 4. Sucesso — evento marcado como concluído, SincronizadoEm preenchido ──

        [Fact(DisplayName = "Offline: venda sincronizada com sucesso marca Outbox e VendasOffline como concluídos")]
        public async Task MarcarEventoSincronizado_AtualizaStatusNasDuasTabelas()
        {
            var vendaId = Guid.NewGuid();
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaId, DtoDeTeste(vendaId));
            var pendentes = await _offlineDb.GetEventosPendentesAsync();

            await _offlineDb.MarcarEventoSincronizadoAsync(pendentes[0].Id, vendaId);

            var pendentesDepois = await _offlineDb.GetEventosPendentesAsync();
            pendentesDepois.Should().BeEmpty("depois de sincronizado, o evento não deve mais aparecer como pendente");

            var status = await _offlineDb.GetStatusAsync();
            status.VendasPendentes.Should().Be(0);
        }

        // ── SyncEngineService.ProcessarOutboxAsync — integra tudo acima ─────

        [Fact(DisplayName = "SyncEngine: processa a Outbox, chama CreateAsync local, marca sucesso, e um evento com erro não trava os outros")]
        public async Task ProcessarOutboxAsync_SincronizaComSucessoENaoTravaNaFalha()
        {
            var vendaOk    = Guid.NewGuid();
            var vendaFalha = Guid.NewGuid();
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaOk,    DtoDeTeste(vendaOk));
            await _offlineDb.SalvarVendaOfflineComOutboxAsync(vendaFalha, DtoDeTeste(vendaFalha));

            var saleServiceMock = new Mock<ISaleService>();
            saleServiceMock
                .Setup(s => s.CreateAsync(It.Is<CreateSaleDto>(d => d.Id == vendaOk)))
                .ReturnsAsync(new SaleDto(vendaOk, "PDV-001", null, null, DateTime.Now, ERP.Domain.Enums.SaleStatus.SemNota, "Dinheiro", 10m));
            saleServiceMock
                .Setup(s => s.CreateAsync(It.Is<CreateSaleDto>(d => d.Id == vendaFalha)))
                .ThrowsAsync(new InvalidOperationException("Produto inexistente"));

            var productServiceMock  = new Mock<IProductService>();
            var customerServiceMock = new Mock<ICustomerService>();
            var motorFinanceiroMock = new Mock<IMotorFinanceiroService>();

            var engine = new SyncEngineService(_offlineDb, saleServiceMock.Object, productServiceMock.Object, customerServiceMock.Object, motorFinanceiroMock.Object);
            var sincronizados = await engine.ProcessarOutboxAsync();

            sincronizados.Should().Be(1, "só a venda sem erro deve contar como sincronizada nessa passada");

            var pendentes = await _offlineDb.GetEventosPendentesAsync();
            pendentes.Should().HaveCount(1, "a venda com erro continua pendente pro próximo ciclo");

            var status = await _offlineDb.GetStatusAsync();
            status.VendasComErro.Should().Be(1);
        }
    }

    /// <summary>
    /// Fase 1 — 1. Idempotência (via SaleService.CreateAsync, §7 do
    /// documento). Reusa exatamente o mesmo padrão de mocks de
    /// SaleServiceTests, testando especificamente o novo bloco de
    /// idempotência adicionado no topo do método.
    /// </summary>
    public class SaleServiceIdempotenciaTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IValidator<CreateSaleDto>> _validatorMock;
        private readonly Mock<IHaverService> _haverServiceMock;
        private readonly Mock<IRequestTenant> _tenantMock;
        private readonly SaleService _saleService;

        public SaleServiceIdempotenciaTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _validatorMock = new Mock<IValidator<CreateSaleDto>>();
            _haverServiceMock = new Mock<IHaverService>();
            _tenantMock = new Mock<IRequestTenant>();
            _tenantMock.Setup(t => t.MaxDiscountPercentage).Returns(100m);

            _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateSaleDto>>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new ValidationResult());

            _saleService = new SaleService(_uowMock.Object, _mapperMock.Object, _validatorMock.Object, _haverServiceMock.Object, _tenantMock.Object);
        }

        [Fact(DisplayName = "Idempotência: Id novo (nunca sincronizado) segue o fluxo normal de criação")]
        public async Task CreateAsync_IdNovo_NaoEntraNoAtalhoDeIdempotencia()
        {
            var idNovo = Guid.NewGuid();
            _uowMock.Setup(u => u.Sales.GetByIdAsync(idNovo)).ReturnsAsync((Sale?)null);

            var dto = new CreateSaleDto { Id = idNovo, UsuarioId = Guid.NewGuid(), Items = new(), Payments = new() };

            // Sem mais mocks de estoque/caixa/transação configurados de propósito:
            // o teste só precisa provar que passou pela checagem de idempotência
            // (chamou GetByIdAsync) e seguiu adiante — não precisa chegar até o fim.
            try { await _saleService.CreateAsync(dto); } catch { /* esperado: faltam outros mocks pra ir até o fim */ }

            _uowMock.Verify(u => u.Sales.GetByIdAsync(idNovo), Times.Once,
                "a checagem de idempotência deve rodar sempre que Id vier preenchido");
        }

        [Fact(DisplayName = "Idempotência: Id que já existe devolve a venda existente, NUNCA cria de novo")]
        public async Task CreateAsync_IdJaExistente_DevolveExistenteSemCriarDuplicata()
        {
            var idJaSincronizado = Guid.NewGuid();
            var vendaExistente = new Sale { Id = idJaSincronizado, SaleNumber = "PDV-042" };
            var dtoEsperado = new SaleDto(idJaSincronizado, "PDV-042", null, null, DateTime.Now, ERP.Domain.Enums.SaleStatus.SemNota, "Dinheiro", 10m);

            _uowMock.Setup(u => u.Sales.GetByIdAsync(idJaSincronizado)).ReturnsAsync(vendaExistente);
            _mapperMock.Setup(m => m.Map<SaleDto>(vendaExistente)).Returns(dtoEsperado);

            var dto = new CreateSaleDto { Id = idJaSincronizado, UsuarioId = Guid.NewGuid(), Items = new(), Payments = new() };

            var resultado = await _saleService.CreateAsync(dto);

            resultado.Should().BeEquivalentTo(dtoEsperado);

            // O ponto central do teste: nunca deve tentar inserir uma venda nova
            // pra esse Id — nem chegar perto da lógica de baixa de estoque/caixa.
            _uowMock.Verify(u => u.Sales.AddAsync(It.IsAny<Sale>()), Times.Never,
                "venda já sincronizada não pode gerar uma segunda inserção — é exatamente o cenário do §17 (resposta perdida)");
            _uowMock.Verify(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()), Times.Never,
                "nem deve entrar na transação de criação — o retorno é imediato, antes de qualquer efeito colateral");
        }
    }
}