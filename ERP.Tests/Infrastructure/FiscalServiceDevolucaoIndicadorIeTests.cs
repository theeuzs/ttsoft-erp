// ERP.Tests/Infrastructure/FiscalServiceDevolucaoIndicadorIeTests.cs
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
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// Achados testando devolução de verdade pela primeira vez, em produção
/// (Fase C, módulo Fiscal) — dois bugs, achados em sequência, na mesma
/// causa raiz: MontarRequestNfeDevolucao é um método separado de
/// MontarRequestNfeA4/MontarRequestNfce, e nunca teve os mesmos fixes.
/// (1) Mesmo bug do S20 (13/08, ver FiscalServiceIndicadorIeTests) — sem
/// IndicadorIeDestinatario, Focus rejeitava com "Missing child element(s).
/// Expected is ( indIEDest )". Devolução tem cliente sempre identificado
/// (DevolucaoViewModel exige isso antes de confirmar, S17), então batia
/// nisso 100% das vezes.
/// (2) Achado no painel da Focus depois do fix acima: codigo_ncm saía
/// sempre "00000000" — o método recebia só a tupla (ProductId, Nome,
/// Quantidade, Valor), nunca o Product de verdade com NCM/CSOSN. Corrigido
/// buscando os produtos antes de montar o request, mesmo padrão do
/// MontarItens (fluxo normal).
/// </summary>
public class FiscalServiceDevolucaoIndicadorIeTests
{
    private static (FiscalService Service, Mock<INfeEmissionService> NfeMock, IServiceScope Scope, Guid ProdutoId)
        Build(Guid tenantId, Guid vendaId, Guid customerId, string? ncm = null, string? csosn = null)
    {
        var produtoId = Guid.NewGuid();
        var provider = TestDb.Create(dbName: $"fiscal_dev_ie_{Guid.NewGuid():N}", tenantId: tenantId, seed: ctx =>
        {
            var produto = new Product
            {
                Id = produtoId, Name = "Produto Teste", TenantId = tenantId, SalePrice = 10m,
                NCM = ncm, CSOSN = csosn
            };
            ctx.Products.Add(produto);

            var customer = new Customer
            {
                Id = customerId, TenantId = tenantId, Name = "Cliente Teste",
                Document = "10087600994", StateRegistration = null
            };
            ctx.Customers.Add(customer);

            ctx.Sales.Add(new Sale
            {
                Id = vendaId, TenantId = tenantId, SaleNumber = "TESTE-DEV-IE",
                SaleDate = DateTime.Now, CustomerId = customerId, Customer = customer,
                // Precisa de uma chave válida — EmitirNotaDevolucaoAsync bloqueia sem isso.
                NfceChave = "41260912820608000141650010000033031335484324",
                Items = new List<SaleItem>
                {
                    new() { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = produtoId, Product = produto,
                            ProductName = produto.Name, Quantity = 1, UnitPrice = 10m, TotalItem = 10m }
                }
            });
        });

        var scope = provider.CreateScope();
        var ctx0  = scope.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();

        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfeMock = new Mock<INfeEmissionService>();
        nfeMock.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((false, "Nota Rejeitada. Status: erro_autorizacao", "", "", "", ""));

        var service = new FiscalService(
            ctx0, configProvider.Object,
            new Mock<INfceEmissionService>().Object,
            nfeMock.Object,
            new Mock<INfeContingencyService>().Object,
            new Mock<ISaleService>().Object);

        return (service, nfeMock, scope, produtoId);
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — cliente identificado por CPF recebe indicador Não Contribuinte, igual ao S20 na emissão normal")]
    public async Task Devolucao_ClienteComCpf_RecebeIndicadorNaoContribuinte()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, produtoId) = Build(tenantId, vendaId, customerId);
        using (scope)
        {
            var itens = new List<(Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (produtoId, "Produto Teste", 1m, 10m) };

            await service.EmitirNotaDevolucaoAsync(vendaId, itens, "Produto com defeito");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.IndicadorIeDestinatario == "9"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — achado no painel da Focus: NCM/CSOSN do produto de verdade, não hardcoded")]
    public async Task Devolucao_UsaNcmECsosnDeVerdadeDoProduto()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, produtoId) = Build(tenantId, vendaId, customerId, ncm: "3917.23.00", csosn: "102-Tributada");
        using (scope)
        {
            var itens = new List<(Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (produtoId, "Joelho 32mm 90 graus", 1m, 2.90m) };

            await service.EmitirNotaDevolucaoAsync(vendaId, itens, "Produto com defeito");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.Itens[0].CodigoNcm == "39172300" &&              // pontos removidos
                    req.Itens[0].IcmsSituacaoTributaria == "102"),       // só o código, sem a descrição
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — produto sem NCM/CSOSN cadastrado cai no fallback, sem quebrar (mesmo comportamento do fluxo normal)")]
    public async Task Devolucao_ProdutoSemNcmCsosn_CaiNoFallback()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, produtoId) = Build(tenantId, vendaId, customerId, ncm: null, csosn: null);
        using (scope)
        {
            var itens = new List<(Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (produtoId, "Produto Teste", 1m, 10m) };

            await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.Itens[0].CodigoNcm == "00000000" &&
                    req.Itens[0].IcmsSituacaoTributaria == "102"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }
}