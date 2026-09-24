// ERP.Tests/Infrastructure/FiscalServiceVUnComTests.cs
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
/// S18 FIX (13/08) — rejeição SEFAZ 629 real na Vila Verde (TUBO 50 ESGOTO
/// 1M, venda de atacado: 6 MT por R$59,90 o pacote fechado). Causa: o XML
/// mandava valor_unitario_comercial=9,98 (item.UnitPrice já arredondado
/// pra 2 casas em outro lugar) e valor_bruto=59,90 (item.TotalPrice) —
/// 6×9,98=59,88≠59,90.
///
/// Correção: ValorUnitarioComercial passa a ser derivado de
/// ValorBruto/Quantidade com 10 casas decimais na hora de montar o
/// payload da Focus (MontarItens em FiscalService.cs), em vez de
/// reformatar item.UnitPrice. A SEFAZ permite até 10 casas em vUnCom —
/// campo meramente informativo (Manual de Orientação do Contribuinte
/// v6.0, pág. 184) — e a própria regra de validação deriva o valor
/// unitário do total, não o contrário. ValorBruto continua vindo de
/// item.TotalPrice sem nenhuma mudança — SaleService, DescontoPolicy e o
/// cálculo de atacado não são tocados por este fix.
/// </summary>
public class FiscalServiceVUnComTests
{
    private static (FiscalService Service, Mock<INfceEmissionService> NfceMock, IServiceScope Scope)
        Build(Guid tenantId, Guid vendaId, decimal quantidade, decimal unitPrice, decimal totalItem)
    {
        var provider = TestDb.Create(dbName: $"fiscal_{Guid.NewGuid():N}", tenantId: tenantId, seed: ctx =>
        {
            var produto = new Product
            {
                Id        = Guid.NewGuid(),
                Name      = "TUBO 50 ESGOTO 1M",
                TenantId  = tenantId,
                SalePrice = 12.50m
            };
            ctx.Products.Add(produto);

            var venda = new Sale
            {
                Id         = vendaId,
                TenantId   = tenantId,
                SaleNumber = "TESTE-S18",
                SaleDate   = DateTime.Now,
                Items = new List<SaleItem>
                {
                    new SaleItem
                    {
                        Id          = Guid.NewGuid(),
                        TenantId    = tenantId,
                        ProductId   = produto.Id,
                        Product     = produto,
                        ProductName = produto.Name,
                        Quantity    = quantidade,
                        UnitPrice   = unitPrice,
                        TotalItem   = totalItem
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

        var nfceMock = new Mock<INfceEmissionService>();
        nfceMock.Setup(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((false, "Nota Rejeitada. Status: erro_autorizacao", "", "", "", ""));

        var service = new FiscalService(
            ctx0,
            configProvider.Object,
            nfceMock.Object,
            new Mock<INfeEmissionService>().Object,
            new Mock<INfeContingencyService>().Object,
            new Mock<ISaleService>().Object);

        return (service, nfceMock, scope);
    }

    [Theory(DisplayName = "vUnCom derivado de ValorBruto/Quantidade com 10 casas — tabela de cenários (S18)")]
    [InlineData(6, 59.90, "9.9833333333")]   // Atacado 6m — o caso real da rejeição
    [InlineData(6, 60.00, "10.0000000000")]  // Preço exato
    [InlineData(6, 59.88, "9.9800000000")]   // Divisão exata (2 casas já batem)
    [InlineData(3, 29.95, "9.9833333333")]   // Meia barra
    [InlineData(1, 12.50, "12.5000000000")]  // Venda avulsa, 1 metro
    public async Task MontarItens_DerivaValorUnitarioComercialComPrecisaoAlta(
        decimal quantidade, decimal totalItem, string vUnComEsperado)
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();
        // UnitPrice simula o valor "equivalente" de 2 casas já persistido
        // (o que o cupom mostra) — propositalmente diferente do que o
        // fix deve calcular, pra provar que o payload usa o total/quantidade,
        // não mais o UnitPrice já arredondado.
        decimal unitPriceEquivalente = Math.Round(totalItem / quantidade, 2, MidpointRounding.AwayFromZero);

        var (service, nfceMock, scope) = Build(tenantId, vendaId, quantidade, unitPriceEquivalente, totalItem);
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFCE");

            nfceMock.Verify(s => s.EmitirNfceAsync(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.Itens.Count == 1 &&
                    req.Itens[0].QuantidadeComercial == quantidade.ToString("F2", CultureInfo.InvariantCulture) &&
                    req.Itens[0].ValorBruto == totalItem.ToString("F2", CultureInfo.InvariantCulture) &&
                    req.Itens[0].ValorUnitarioComercial == vUnComEsperado),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "ValorBruto nunca muda de valor — permanece exatamente o total comercial da venda (S18)")]
    public async Task MontarItens_ValorBrutoPermaneceIgualAoTotalComercial()
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();

        var (service, nfceMock, scope) = Build(tenantId, vendaId, quantidade: 6m, unitPrice: 9.98m, totalItem: 59.90m);
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFCE");

            nfceMock.Verify(s => s.EmitirNfceAsync(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.Itens[0].ValorBruto == "59.90"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }
}