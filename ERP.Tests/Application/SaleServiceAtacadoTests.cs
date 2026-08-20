// ERP.Tests/Application/SaleServiceAtacadoTests.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Helpers;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace ERP.Tests.Application.Services;

/// <summary>
/// S18 FIX real (13/08) — "barra cravada". Achado a partir do incidente
/// real de rejeição fiscal na Vila Verde + confirmação direta do dono do
/// sistema: WholesalePrice é o preço do PACOTE/barra inteira fechada
/// (ex: barra de 6m por R$59,90 no total), não preço por unidade.
///
/// O SaleService.CreateAsync antigo fazia `unitPrice = product.WholesalePrice`
/// e multiplicava pela Quantity inteira -- cobrava 6 barras de R$59,90
/// como 6 x R$59,90 = R$359,40 em vez de R$59,90 pela barra de 6 metros.
/// Bug real de dinheiro (overcharge), não só de centavos -- e ativo desde
/// que a Fase B colocou HttpSaleService/API em produção.
/// </summary>
public class DescontoPolicyAtacadoTests
{
    [Fact(DisplayName = "CalcularTotalAtacado - Quantidade exata do pacote cobra o preco do pacote, nao o preco vezes a quantidade")]
    public void CalcularTotalAtacado_QuantidadeExataDoPacote_CobraPrecoDoPacote()
    {
        // Reproduz o incidente: barra de 6m, pacote fechado R$59,90.
        var (total, precoEquivalente) = DescontoPolicy.CalcularTotalAtacado(
            quantity: 6m, wholesaleMinQuantity: 6m, wholesalePrice: 59.90m,
            normalUnitPrice: 11.90m, discountPercent: 0m);

        total.Should().Be(59.90m, "1 barra fechada de 6m custa R$59,90 no total, nao 6x R$59,90");
        precoEquivalente.Should().Be(9.98m, "59,90 / 6 = 9,9833... arredondado pra 9,98, so pra exibicao/fiscal");
    }

    [Fact(DisplayName = "CalcularTotalAtacado - Bug antigo teria cobrado 6x mais (regressao)")]
    public void CalcularTotalAtacado_NaoReproduzOBugDeMultiplicarPacotePelaQuantidadeToda()
    {
        var (total, _) = DescontoPolicy.CalcularTotalAtacado(
            quantity: 6m, wholesaleMinQuantity: 6m, wholesalePrice: 59.90m,
            normalUnitPrice: 11.90m, discountPercent: 0m);

        total.Should().NotBe(59.90m * 6m); // R$359,40 -- o valor que o bug antigo cobrava
    }

    [Fact(DisplayName = "CalcularTotalAtacado - Duas barras fechadas cobra 2x o preco do pacote")]
    public void CalcularTotalAtacado_DuasBarrasFechadas_CobraDoisPacotes()
    {
        var (total, _) = DescontoPolicy.CalcularTotalAtacado(
            quantity: 12m, wholesaleMinQuantity: 6m, wholesalePrice: 59.90m,
            normalUnitPrice: 11.90m, discountPercent: 0m);

        total.Should().Be(119.80m); // 2 x 59,90
    }

    [Fact(DisplayName = "CalcularTotalAtacado - Sobra fracionaria usa o preco normal so na sobra")]
    public void CalcularTotalAtacado_ComSobra_UsaPrecoNormalNaSobra()
    {
        // 1 barra fechada (6m a R$59,90) + 2m soltos a preco normal (R$11,90/m)
        var (total, _) = DescontoPolicy.CalcularTotalAtacado(
            quantity: 8m, wholesaleMinQuantity: 6m, wholesalePrice: 59.90m,
            normalUnitPrice: 11.90m, discountPercent: 0m);

        total.Should().Be(59.90m + 2m * 11.90m); // 83,70
    }

    [Fact(DisplayName = "CalcularTotalAtacado - Desconto se aplica sobre o total, nao sobre o preco do pacote isolado")]
    public void CalcularTotalAtacado_ComDesconto_AplicaSobreOTotal()
    {
        var (total, _) = DescontoPolicy.CalcularTotalAtacado(
            quantity: 6m, wholesaleMinQuantity: 6m, wholesalePrice: 59.90m,
            normalUnitPrice: 11.90m, discountPercent: 10m);

        total.Should().Be(59.90m * 0.9m); // 53,91
    }
}

public class SaleServiceAtacadoTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IValidator<CreateSaleDto>> _validatorMock;
    private readonly Mock<IHaverService> _haverServiceMock;
    private readonly Mock<IRequestTenant> _tenantMock;
    private readonly SaleService _saleService;
    private Sale? _vendaCapturada;

    public SaleServiceAtacadoTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _validatorMock = new Mock<IValidator<CreateSaleDto>>();
        _haverServiceMock = new Mock<IHaverService>();

        _tenantMock = new Mock<IRequestTenant>();
        _tenantMock.Setup(t => t.MaxDiscountPercentage).Returns(100m);

        _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateSaleDto>>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new ValidationResult());

        _uowMock.Setup(u => u.Sales.AddAsync(It.IsAny<Sale>()))
                .Callback<Sale>(s => _vendaCapturada = s)
                .Returns(Task.CompletedTask);

        _uowMock.Setup(u => u.Products.BaixarEstoqueAtomicoAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

        _uowMock.Setup(u => u.ContasReceber.GetBySaleIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<ContaReceber>());

        _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(0);

        var txMock = new Mock<ITransaction>();
        txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        txMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        txMock.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _uowMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(txMock.Object);

        _uowMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
            .Returns((Func<Task> operacao) => operacao());

        _saleService = new SaleService(_uowMock.Object, _mapperMock.Object, _validatorMock.Object, _haverServiceMock.Object, _tenantMock.Object);
    }

    [Fact(DisplayName = "CriarVenda - Produto de atacado (barra fechada) cobra o preco do pacote, nao pacote x quantidade (incidente real Vila Verde)")]
    [Trait("Categoria", "Vendas - Atacado")]
    public async Task CriarVenda_ProdutoDeAtacadoNaQuantidadeExataDoPacote_CobraPrecoDoPacote()
    {
        // Arrange — reproduz o TUBO 50 ESGOTO 1M: barra de 6m, R$59,90 o pacote fechado.
        var usuarioId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var produtoFake = new Product
        {
            Id                   = produtoId,
            Name                 = "TUBO 50 ESGOTO 1M",
            Stock                = 100,
            SalePrice            = 11.90m, // preco normal, por metro avulso
            WholesaleMinQuantity = 6m,
            WholesalePrice       = 59.90m  // preco da barra INTEIRA de 6m
        };

        var dto = new CreateSaleDto
        {
            UsuarioId = usuarioId,
            Items     = new List<CreateSaleItemDto> { new() { ProductId = produtoId, Quantity = 6 } },
            Payments  = new List<CreateSalePaymentDto>
            {
                new() { PaymentMethod = ERP.Domain.Enums.PaymentMethod.CartaoDebito, Amount = 59.90m }
            }
        };

        _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
        _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);
        _mapperMock.Setup(m => m.Map<SaleDto>(It.IsAny<Sale>())).Returns((SaleDto)null!);

        // Act
        await _saleService.CreateAsync(dto);

        // Assert
        _vendaCapturada.Should().NotBeNull();
        var item = _vendaCapturada!.Items.Should().ContainSingle().Subject;

        item.TotalItem.Should().Be(59.90m,
            "a barra fechada de 6m custa R$59,90 no total -- o bug antigo cobraria R$359,40 (6 x R$59,90)");
        item.UnitPrice.Should().Be(9.98m,
            "preco unitario e so o valor equivalente pra cupom/fiscal (59,90 / 6), nao o preco real de venda por unidade");
    }

    [Fact(DisplayName = "CriarVenda - Produto de atacado com sobra fracionaria soma pacote fechado + sobra no preco normal")]
    [Trait("Categoria", "Vendas - Atacado")]
    public async Task CriarVenda_ProdutoDeAtacadoComSobra_SomaPacoteMaisSobraNoPrecoNormal()
    {
        var usuarioId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var produtoFake = new Product
        {
            Id                   = produtoId,
            Name                 = "TUBO 50 ESGOTO 1M",
            Stock                = 100,
            SalePrice            = 11.90m,
            WholesaleMinQuantity = 6m,
            WholesalePrice       = 59.90m
        };

        // 8 metros: 1 barra fechada (6m a R$59,90) + 2m soltos (R$11,90/m) = R$83,70
        var dto = new CreateSaleDto
        {
            UsuarioId = usuarioId,
            Items     = new List<CreateSaleItemDto> { new() { ProductId = produtoId, Quantity = 8 } },
            Payments  = new List<CreateSalePaymentDto>
            {
                new() { PaymentMethod = ERP.Domain.Enums.PaymentMethod.Dinheiro, Amount = 83.70m }
            }
        };

        _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
        _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);
        _mapperMock.Setup(m => m.Map<SaleDto>(It.IsAny<Sale>())).Returns((SaleDto)null!);

        await _saleService.CreateAsync(dto);

        _vendaCapturada.Should().NotBeNull();
        var item = _vendaCapturada!.Items.Should().ContainSingle().Subject;

        item.TotalItem.Should().Be(83.70m);
    }

    [Fact(DisplayName = "CriarVenda - Produto sem atacado configurado continua usando o preco normal x quantidade (nao regrediu)")]
    [Trait("Categoria", "Vendas - Atacado")]
    public async Task CriarVenda_ProdutoSemAtacado_ContinuaUsandoPrecoNormal()
    {
        var usuarioId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var produtoFake = new Product
        {
            Id        = produtoId,
            Name      = "Cimento Votorantim",
            Stock     = 100,
            SalePrice = 35m
            // sem WholesalePrice/WholesaleMinQuantity
        };

        var dto = new CreateSaleDto
        {
            UsuarioId = usuarioId,
            Items     = new List<CreateSaleItemDto> { new() { ProductId = produtoId, Quantity = 3 } },
            Payments  = new List<CreateSalePaymentDto>
            {
                new() { PaymentMethod = ERP.Domain.Enums.PaymentMethod.Dinheiro, Amount = 105m }
            }
        };

        _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
        _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);
        _mapperMock.Setup(m => m.Map<SaleDto>(It.IsAny<Sale>())).Returns((SaleDto)null!);

        await _saleService.CreateAsync(dto);

        _vendaCapturada.Should().NotBeNull();
        var item = _vendaCapturada!.Items.Should().ContainSingle().Subject;

        item.UnitPrice.Should().Be(35m);
        item.TotalItem.Should().Be(105m);
    }

    [Fact(DisplayName = "CriarVenda - Frete (S24) entra no Total da venda")]
    [Trait("Categoria", "Vendas - Frete")]
    public async Task CriarVenda_ComFrete_EntraNoTotal()
    {
        var usuarioId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var produtoFake = new Product
        {
            Id = produtoId, Name = "Disco de Corte", Stock = 100, SalePrice = 2.99m
        };

        // Caso real: 10 discos a R$2,99 (R$29,90) + frete R$19,99 = R$49,89
        var dto = new CreateSaleDto
        {
            UsuarioId      = usuarioId,
            ShippingValue  = 19.99m,
            Items          = new List<CreateSaleItemDto> { new() { ProductId = produtoId, Quantity = 10 } },
            Payments       = new List<CreateSalePaymentDto>
            {
                new() { PaymentMethod = ERP.Domain.Enums.PaymentMethod.Pix, Amount = 49.89m }
            }
        };

        _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
        _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);
        _mapperMock.Setup(m => m.Map<SaleDto>(It.IsAny<Sale>())).Returns((SaleDto)null!);

        await _saleService.CreateAsync(dto);

        _vendaCapturada.Should().NotBeNull();
        _vendaCapturada!.ShippingValue.Should().Be(19.99m);
        _vendaCapturada!.Subtotal.Should().Be(29.90m);
        _vendaCapturada!.Total.Should().Be(49.89m);
    }
}