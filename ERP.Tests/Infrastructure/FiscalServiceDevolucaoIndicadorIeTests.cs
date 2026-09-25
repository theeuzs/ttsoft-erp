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
/// (Fase C, módulo Fiscal). Quatro achados, na mesma área de código:
/// (1) S20 replicado — IndicadorIeDestinatario faltando (rejeição
/// "Missing... indIEDest"); (2) NCM/CSOSN hardcoded, não o produto real;
/// (3) TipoDocumento errado (rejeição SEFAZ 518, "CFOP de entrada para NF-e
/// de saida"); (4) regra VC02-14 (NT 2025.002-RTC, produção 01/09/2026) —
/// referenciamento por item (chave_acesso_dfe_referenciado +
/// numero_item_dfe_referenciado), não mais só no cabeçalho — rejeição
/// "DFe Referenciado nao informado". Essa última exigiu SaleItem.NumeroItemFiscal
/// novo + SaleItemId em toda a cadeia de devolução (design review aprovado
/// antes da implementação).
/// </summary>
public class FiscalServiceDevolucaoIndicadorIeTests
{
    private static (FiscalService Service, Mock<INfeEmissionService> NfeMock, IServiceScope Scope, Guid SaleItemId, Guid ProdutoId)
        Build(Guid tenantId, Guid vendaId, Guid customerId, string? ncm = null, string? csosn = null, int? numeroItemFiscal = 1)
    {
        var produtoId  = Guid.NewGuid();
        var saleItemId = Guid.NewGuid();
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
                    new() { Id = saleItemId, TenantId = tenantId, ProductId = produtoId, Product = produto,
                            ProductName = produto.Name, Quantity = 1, UnitPrice = 10m, TotalItem = 10m,
                            NumeroItemFiscal = numeroItemFiscal }
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

        return (service, nfeMock, scope, saleItemId, produtoId);
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — cliente identificado por CPF recebe indicador Não Contribuinte, igual ao S20 na emissão normal")]
    public async Task Devolucao_ClienteComCpf_RecebeIndicadorNaoContribuinte()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

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
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId, ncm: "3917.23.00", csosn: "102-Tributada");
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Joelho 32mm 90 graus", 1m, 2.90m) };

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
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId, ncm: null, csosn: null);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

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

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — achado na rejeição SEFAZ 518 real: TipoDocumento precisa ser Entrada (0), consistente com o CFOP 1202")]
    public async Task Devolucao_TipoDocumentoEEntrada_ConsistenteComCfop()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

            await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.TipoDocumento == "0" && req.Itens[0].Cfop == "1202"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    // ── Regra VC02-14 — referenciamento por item ────────────────────────

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — payload tem chave_acesso_dfe_referenciado + numero_item_dfe_referenciado por item")]
    public async Task Devolucao_PayloadTemReferenciaFiscalPorItem()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId, numeroItemFiscal: 3);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

            await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.Itens[0].ChaveAcessoDfeReferenciado == "41260912820608000141650010000033031335484324" &&
                    req.Itens[0].NumeroItemDfeReferenciado  == "3"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — payload NÃO tem notas_referenciadas no cabeçalho (não pode coexistir com referência por item)")]
    public async Task Devolucao_PayloadNaoTemNotasReferenciadasNoCabecalho()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

            await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.NotasReferenciadas == null || req.NotasReferenciadas.Count == 0),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — SaleItemId inexistente na venda recusa com mensagem clara, não chama a Focus")]
    public async Task Devolucao_SaleItemIdInexistente_RecusaSemChamarFocus()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, _, produtoId) = Build(tenantId, vendaId, customerId);
        using (scope)
        {
            var saleItemIdErrado = Guid.NewGuid(); // não existe na venda
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemIdErrado, produtoId, "Produto Teste", 1m, 10m) };

            var resultado = await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("não corresponde a nenhuma linha da venda");
            nfeMock.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — NumeroItemFiscal null (venda antiga sem backfill) recusa com mensagem clara, não chama a Focus")]
    public async Task Devolucao_NumeroItemFiscalNulo_RecusaSemChamarFocus()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId, numeroItemFiscal: null);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

            var resultado = await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("rastreamento de item por número");
            nfeMock.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — venda antiga já com backfill (NumeroItemFiscal preenchido manualmente) funciona normalmente")]
    public async Task Devolucao_VendaAntigaComBackfill_FuncionaNormalmente()
    {
        var tenantId   = Guid.NewGuid();
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        // numeroItemFiscal: 1, igual ao backfill real das 2 vendas históricas
        // (ambas com 1 item só) — simula exatamente esse cenário.
        var (service, nfeMock, scope, saleItemId, produtoId) = Build(tenantId, vendaId, customerId, numeroItemFiscal: 1);
        using (scope)
        {
            var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
                { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

            var resultado = await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

            // Rejeitada pelo mock (setup padrão), mas o ponto do teste é que
            // CHEGOU a chamar a Focus — não foi bloqueada pela validação
            // defensiva, como aconteceria sem o backfill.
            nfeMock.Verify(s => s.EmitirNfeA4Async(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.Itens[0].NumeroItemDfeReferenciado == "1"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }
}