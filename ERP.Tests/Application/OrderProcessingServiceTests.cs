// ERP.Tests/Application/OrderProcessingServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// Motor de processamento de pedidos de marketplace, sem cobertura de teste
/// nenhuma até aqui (achado real da auditoria de 13/08: "motores críticos
/// sem rede"). Prioriza as três propriedades mais perigosas de errar,
/// documentadas nos próprios comentários do código-fonte:
///
/// 1. ContaReceberService.GerarContaAPrazoAsync tem que usar pedido.ValorTotal
///    (o que o marketplace cobrou de verdade), NUNCA saleDto.Total (que o
///    SaleService recalcula pela tabela de preço local — pode divergir).
/// 2. Se pedido.VendaId já existe (tentativa anterior), NUNCA chamar
///    _saleService.CreateAsync de novo — isso baixaria estoque em dobro.
/// 3. Falha de emissão fiscal é best-effort: nunca pode travar/reverter um
///    pedido real do marketplace (nem quando o serviço fiscal nem está
///    configurado — _fiscalService pode ser null).
/// </summary>
public class OrderProcessingServiceTests
{
    private static readonly Guid CanalId  = Guid.NewGuid();
    private static readonly Guid UsuarioIntegracaoId = Guid.NewGuid();
    private const string ExternalOrderId = "PEDIDO-EXTERNO-1";
    private const string Sku = "SKU-DISCO-CORTE";

    private class Fixture
    {
        public Mock<IUnitOfWork> Uow = new();
        public Mock<IOrderSyncRepository> OrderSync = new();
        public Mock<IProductRepository> Products = new();
        public Mock<ISaleService> SaleService = new();
        public Mock<IContaReceberService> ContaReceber = new();
        public Mock<IChannelDispatcher> Dispatcher = new();
        public Mock<IFiscalService> Fiscal = new();
        public OrderProcessingService Service = null!;

        public static Fixture Build(bool comFiscal = true)
        {
            var f = new Fixture();
            f.Uow.Setup(u => u.OrderSync).Returns(f.OrderSync.Object);
            f.Uow.Setup(u => u.Products).Returns(f.Products.Object);
            f.Dispatcher.Setup(d => d.Tipo).Returns(SalesChannelType.MercadoLivre);

            f.Service = new OrderProcessingService(
                f.Uow.Object, f.SaleService.Object, f.ContaReceber.Object,
                new[] { f.Dispatcher.Object }, comFiscal ? f.Fiscal.Object : null);

            return f;
        }
    }

    private static SalesChannel Canal() => new()
    {
        Id = CanalId, Tipo = SalesChannelType.MercadoLivre, UsuarioIntegracaoId = UsuarioIntegracaoId, Nome = "ML Loja"
    };

    private static ExternalOrderDto PedidoDto(decimal valorTotal) => new()
    {
        ExternalOrderId   = ExternalOrderId,
        ExternalStatus    = "paid",
        DataPedidoExterno = DateTime.UtcNow,
        ValorTotal        = valorTotal,
        Itens = new List<ExternalOrderItemDto>
        {
            new() { SkuExterno = Sku, ItemId = "MLB1", DescricaoItem = "Disco de Corte", Quantidade = 10, ValorUnitario = 2.99m }
        }
    };

    /// <summary>Configura o caminho feliz até "pedido novo, sem SKU resolvido
    /// ainda" — cada teste sobrescreve o que precisar a partir daqui.</summary>
    private static void ConfigurarBase(Fixture f, Guid productId, decimal stock = 100, decimal reservado = 0, decimal bufferSeguranca = 0)
    {
        f.OrderSync.Setup(o => o.GetCanalByIdAsync(CanalId)).ReturnsAsync(Canal());
        f.OrderSync.Setup(o => o.GetExternalOrderAsync(CanalId, ExternalOrderId)).ReturnsAsync((ExternalOrder?)null);
        f.OrderSync.Setup(o => o.TentarInserirExternalOrderAsync(It.IsAny<ExternalOrder>())).ReturnsAsync(true);
        f.OrderSync.Setup(o => o.GetSkuMappingAsync(CanalId, Sku))
            .ReturnsAsync(new SkuMapping { ProductId = productId, BufferSeguranca = bufferSeguranca });
        f.Products.Setup(p => p.GetByIdAsync(productId))
            .ReturnsAsync(new Product { Id = productId, Name = "Disco de Corte", Stock = stock });
        f.OrderSync.Setup(o => o.GetTotalReservadoAsync(productId)).ReturnsAsync(reservado);
        f.OrderSync.Setup(o => o.GetReservasAtivasPorPedidoAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<ShadowStockReservation>());
        f.OrderSync.Setup(o => o.GetClienteRepasseAsync(CanalId)).ReturnsAsync((Customer?)null);
        f.OrderSync.Setup(o => o.CriarClienteRepasseAsync(It.IsAny<SalesChannel>()))
            .ReturnsAsync(new Customer { Id = Guid.NewGuid(), Name = "Repasse ML" });
    }

    [Fact(DisplayName = "Fluxo completo: ContaReceber usa pedido.ValorTotal, NUNCA saleDto.Total (podem divergir)")]
    public async Task FluxoCompleto_ContaReceberUsaValorDoMarketplace_NaoOTotalRecalculado()
    {
        var f = Fixture.Build();
        var productId = Guid.NewGuid();
        var vendaId = Guid.NewGuid();
        const decimal valorRealDoPedido = 49.89m; // produtos + frete, o que o ML cobrou de verdade
        const decimal totalRecalculadoPeloSaleService = 999m; // propositalmente diferente, pra provar que NÃO é usado

        ConfigurarBase(f, productId);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(valorRealDoPedido)));
        f.SaleService.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "VND-1", null, null, DateTime.Now, SaleStatus.SemNota, "APrazo", totalRecalculadoPeloSaleService));
        f.Fiscal.Setup(x => x.EmitirNotaAsync(vendaId, "NFCE"))
            .ReturnsAsync(new FiscalEmissionResult { Sucesso = true, Status = "Autorizada" });

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), vendaId, valorRealDoPedido, It.IsAny<string>(), null), Times.Once);
        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), vendaId, totalRecalculadoPeloSaleService, It.IsAny<string>(), null), Times.Never);
    }

    [Fact(DisplayName = "Emissão fiscal falhando não impede a Conta a Receber (best-effort)")]
    public async Task EmissaoFiscalFalhando_NaoImpedeContaReceber()
    {
        var f = Fixture.Build();
        var productId = Guid.NewGuid();
        var vendaId = Guid.NewGuid();

        ConfigurarBase(f, productId);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));
        f.SaleService.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "VND-1", null, null, DateTime.Now, SaleStatus.SemNota, "APrazo", 49.89m));
        f.Fiscal.Setup(x => x.EmitirNotaAsync(vendaId, "NFCE"))
            .ReturnsAsync(new FiscalEmissionResult { Sucesso = false, Mensagem = "Focus fora do ar" });

        var act = async () => await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        await act.Should().NotThrowAsync("falha fiscal é best-effort, não pode travar o pedido do marketplace");
        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), vendaId, 49.89m, It.IsAny<string>(), null), Times.Once);
    }

    [Fact(DisplayName = "Emissão fiscal lançando exceção não impede a Conta a Receber (best-effort)")]
    public async Task EmissaoFiscalLancandoExcecao_NaoImpedeContaReceber()
    {
        var f = Fixture.Build();
        var productId = Guid.NewGuid();
        var vendaId = Guid.NewGuid();

        ConfigurarBase(f, productId);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));
        f.SaleService.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "VND-1", null, null, DateTime.Now, SaleStatus.SemNota, "APrazo", 49.89m));
        f.Fiscal.Setup(x => x.EmitirNotaAsync(vendaId, "NFCE"))
            .ThrowsAsync(new InvalidOperationException("token Focus inválido"));

        var act = async () => await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        await act.Should().NotThrowAsync();
        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), vendaId, 49.89m, It.IsAny<string>(), null), Times.Once);
    }

    [Fact(DisplayName = "Sem IFiscalService configurado (null) — pedido processa normal, sem tentar emitir nada")]
    public async Task SemFiscalService_ProcessaNormalSemTentarEmitir()
    {
        var f = Fixture.Build(comFiscal: false);
        var productId = Guid.NewGuid();
        var vendaId = Guid.NewGuid();

        ConfigurarBase(f, productId);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));
        f.SaleService.Setup(s => s.CreateAsync(It.IsAny<CreateSaleDto>()))
            .ReturnsAsync(new SaleDto(vendaId, "VND-1", null, null, DateTime.Now, SaleStatus.SemNota, "APrazo", 49.89m));

        var act = async () => await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        await act.Should().NotThrowAsync();
        f.Fiscal.Verify(x => x.EmitirNotaAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), vendaId, 49.89m, It.IsAny<string>(), null), Times.Once);
    }

    [Fact(DisplayName = "SKU sem mapeamento — para antes de reservar estoque ou gerar venda, registra conflito")]
    public async Task SkuSemMapeamento_ParaAntesDeGerarVenda()
    {
        var f = Fixture.Build();
        ConfigurarBase(f, Guid.NewGuid());
        f.OrderSync.Setup(o => o.GetSkuMappingAsync(CanalId, Sku)).ReturnsAsync((SkuMapping?)null);
        f.OrderSync.Setup(o => o.GetSkuMappingAsync(CanalId, "MLB1")).ReturnsAsync((SkuMapping?)null);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.SaleService.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Never);
        f.OrderSync.Verify(o => o.AddOrderConflictAsync(
            It.Is<OrderConflict>(c => c.Tipo == OrderConflictType.SkuNaoMapeado)), Times.Once);
    }

    [Fact(DisplayName = "Estoque insuficiente (considerando reservas de outros pedidos) — para antes de gerar venda")]
    public async Task EstoqueInsuficiente_ParaAntesDeGerarVenda()
    {
        var f = Fixture.Build();
        var productId = Guid.NewGuid();
        // Stock 10, já reservado 5 por outro pedido => só 5 disponíveis, pedido quer 10.
        ConfigurarBase(f, productId, stock: 10, reservado: 5);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.SaleService.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Never);
        f.OrderSync.Verify(o => o.AddOrderConflictAsync(
            It.Is<OrderConflict>(c => c.Tipo == OrderConflictType.EstoqueInsuficiente)), Times.Once);
    }

    [Fact(DisplayName = "BufferSeguranca do mapeamento reduz o disponível, mesmo com estoque físico suficiente")]
    public async Task BufferSeguranca_ReduzDisponivel()
    {
        var f = Fixture.Build();
        var productId = Guid.NewGuid();
        // Stock 10, sem reserva de outro pedido, mas buffer de 5 -> só 5 disponíveis, pedido quer 10.
        ConfigurarBase(f, productId, stock: 10, reservado: 0, bufferSeguranca: 5);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.SaleService.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Never);
    }

    [Fact(DisplayName = "Pedido duplicado (corrida entre webhooks) — não reprocessa, para no insert")]
    public async Task PedidoDuplicado_NaoReprocessa()
    {
        var f = Fixture.Build();
        ConfigurarBase(f, Guid.NewGuid());
        f.OrderSync.Setup(o => o.TentarInserirExternalOrderAsync(It.IsAny<ExternalOrder>())).ReturnsAsync(false);
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.OrderSync.Verify(o => o.GetSkuMappingAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        f.SaleService.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Never);
    }

    [Fact(DisplayName = "Pedido já concluído (VendaGerada) — early exit, não faz nada de novo")]
    public async Task PedidoJaComVendaGerada_NaoFazNadaDeNovo()
    {
        var f = Fixture.Build();
        f.OrderSync.Setup(o => o.GetCanalByIdAsync(CanalId)).ReturnsAsync(Canal());
        f.OrderSync.Setup(o => o.GetExternalOrderAsync(CanalId, ExternalOrderId))
            .ReturnsAsync(new ExternalOrder { InternalStatus = ExternalOrderStatus.VendaGerada });
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.OrderSync.Verify(o => o.TentarInserirExternalOrderAsync(It.IsAny<ExternalOrder>()), Times.Never);
        f.SaleService.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Never);
        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), null), Times.Never);
    }

    [Fact(DisplayName = "CRÍTICO: venda já existe de tentativa anterior — NUNCA chama CreateAsync de novo (evita baixa de estoque em dobro)")]
    public async Task VendaJaExisteDeTentativaAnterior_NuncaRecriaAVenda()
    {
        var f = Fixture.Build();
        var productId = Guid.NewGuid();
        var vendaIdExistente = Guid.NewGuid();

        ConfigurarBase(f, productId);
        // Pedido já passou pela reserva de estoque numa tentativa anterior — só
        // falta o repasse (ex: a etapa de ContaReceber falhou da vez passada).
        f.OrderSync.Setup(o => o.GetExternalOrderAsync(CanalId, ExternalOrderId))
            .ReturnsAsync(new ExternalOrder
            {
                Id = Guid.NewGuid(),
                SalesChannelId = CanalId,
                ExternalOrderId = ExternalOrderId,
                VendaId = vendaIdExistente,
                InternalStatus = ExternalOrderStatus.EstoqueReservado,
                ValorTotal = 49.89m,
                Itens = new List<ExternalOrderItem> { new() { SkuExterno = Sku, ItemId = "MLB1", Quantidade = 10, ProductId = productId } }
            });
        f.Dispatcher.Setup(d => d.BuscarPedidoPorIdAsync(It.IsAny<SalesChannel>(), ExternalOrderId))
            .ReturnsAsync((true, "", PedidoDto(49.89m)));
        f.Fiscal.Setup(x => x.EmitirNotaAsync(vendaIdExistente, "NFCE"))
            .ReturnsAsync(new FiscalEmissionResult { Sucesso = true });

        await f.Service.ProcessarPedidoIndividualAsync(CanalId, ExternalOrderId);

        f.SaleService.Verify(s => s.CreateAsync(It.IsAny<CreateSaleDto>()), Times.Never);
        f.ContaReceber.Verify(c => c.GerarContaAPrazoAsync(
            It.IsAny<Guid>(), vendaIdExistente, 49.89m, It.IsAny<string>(), null), Times.Once);
    }
}
