// ERP.Tests/Infrastructure/FiscalServiceFreteTests.cs
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Infrastructure.Services;
using ERP.Tests;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// S24 (17/08) — venda do Mercado Livre com frete cobrado do cliente não
/// tinha como declarar o valor na NF-e A4: ModalidadeFrete vinha fixo em
/// "9" (sem transporte) e não existia campo de valor no payload — a Focus
/// rejeitava por divergência de valores no robô do próprio Mercado Livre.
/// Corrigido: Sale.ShippingValue agora é propriedade real da venda (entra
/// no Total), e MontarRequestNfeA4 declara valor_frete/modalidade_frete
/// quando existe.
/// </summary>
public class FiscalServiceFreteTests
{
    [Fact(DisplayName = "Sale.RecalculateTotals soma o frete no Total")]
    public void RecalculateTotals_SomaFreteNoTotal()
    {
        var sale = new Sale
        {
            ShippingValue = 19.99m,
            DiscountAmount = 0,
            Items = new List<SaleItem>
            {
                new() { Quantity = 1, UnitPrice = 29.90m, TotalItem = 29.90m }
            }
        };

        sale.RecalculateTotals();

        sale.Subtotal.Should().Be(29.90m);
        sale.Total.Should().Be(49.89m); // 29,90 + 19,99 — o caso real do Mercado Livre
    }

    private static (FiscalService Service, Mock<INfeEmissionService> NfeMock, IServiceScope Scope)
        Build(Guid tenantId, Guid vendaId, decimal shippingValue)
    {
        var provider = TestDb.Create(dbName: $"fiscal_frete_{Guid.NewGuid():N}", tenantId: tenantId, seed: ctx =>
        {
            var produto = new Product { Id = Guid.NewGuid(), Name = "Disco de Corte", TenantId = tenantId, SalePrice = 2.99m };
            ctx.Products.Add(produto);

            var customer = new Customer
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cliente ML", Document = "12443095916"
            };
            ctx.Customers.Add(customer);

            var venda = new Sale
            {
                Id            = vendaId,
                TenantId      = tenantId,
                SaleNumber    = "TESTE-S24",
                SaleDate      = DateTime.Now,
                CustomerId    = customer.Id,
                Customer      = customer,
                ShippingValue = shippingValue,
                Items = new List<SaleItem>
                {
                    new SaleItem
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, ProductId = produto.Id, Product = produto,
                        ProductName = produto.Name, Quantity = 10, UnitPrice = 2.99m, TotalItem = 29.90m
                    }
                }
            };
            ctx.Sales.Add(venda);
        });

        var scope = provider.CreateScope();
        var ctx0  = scope.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();

        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfeMock = new Mock<INfeEmissionService>();
        nfeMock.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((false, "Nota Rejeitada. Status: erro_autorizacao", "", ""));

        var service = new FiscalService(
            ctx0,
            configProvider.Object,
            new Mock<INfceEmissionService>().Object,
            nfeMock.Object,
            new Mock<INfeContingencyService>().Object,
            new Mock<ISaleService>().Object);

        return (service, nfeMock, scope);
    }

    [Fact(DisplayName = "Venda com frete declara valor_frete e modalidade 1 (destinatário) na NFe A4")]
    public async Task VendaComFrete_DeclaraValorFreteNaNfeA4()
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();
        var (service, nfeMock, scope) = Build(tenantId, vendaId, shippingValue: 19.99m);
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFE");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.ValorFrete == "19.99" &&
                    req.ModalidadeFrete == "1"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "Venda sem frete mantém modalidade 9 (sem transporte) e não manda valor_frete")]
    public async Task VendaSemFrete_MantemModalidadeSemTransporte()
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();
        var (service, nfeMock, scope) = Build(tenantId, vendaId, shippingValue: 0m);
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFE");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.ValorFrete == null &&
                    req.ModalidadeFrete == "9"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }
}
