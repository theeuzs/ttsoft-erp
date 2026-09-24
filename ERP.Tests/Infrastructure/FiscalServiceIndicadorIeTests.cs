// ERP.Tests/Infrastructure/FiscalServiceIndicadorIeTests.cs
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
/// S20 FIX (13/08) — achado testando o S18/S19 em produção real: a
/// primeira venda com cliente identificado (CPF) depois desses dois fixes
/// quebrou com um erro novo, de schema puro: "Element dest: Missing child
/// element(s). Expected is one of (enderDest, indIEDest)". MontarRequestNfce
/// e MontarRequestNfeA4 nunca preenchiam IndicadorIeDestinatario — a
/// rejeição 629 original nunca tinha exposto isso porque parava antes,
/// no erro de cálculo (o payload nem chegava a ser validado pra esse
/// detalhe). Corrigido: "9" (Não Contribuinte) quando há CPF/CNPJ sem IE
/// cadastrada, "1" (Contribuinte) quando há IE real (só no NFe A4, que
/// já tinha essa informação disponível).
/// </summary>
public class FiscalServiceIndicadorIeTests
{
    private static (FiscalService Service, Mock<INfceEmissionService> NfceMock, IServiceScope Scope)
        Build(Guid tenantId, Guid vendaId, Guid? customerId, string? customerDocument, string? customerStateRegistration = null)
    {
        var provider = TestDb.Create(dbName: $"fiscal_ie_{Guid.NewGuid():N}", tenantId: tenantId, seed: ctx =>
        {
            var produto = new Product { Id = Guid.NewGuid(), Name = "Produto Teste", TenantId = tenantId, SalePrice = 10m };
            ctx.Products.Add(produto);

            Customer? customer = null;
            if (customerId.HasValue)
            {
                customer = new Customer
                {
                    Id                = customerId.Value,
                    TenantId          = tenantId,
                    Name              = "Cliente Teste",
                    Document          = customerDocument ?? string.Empty,
                    StateRegistration = customerStateRegistration
                };
                ctx.Customers.Add(customer);
            }

            var venda = new Sale
            {
                Id         = vendaId,
                TenantId   = tenantId,
                SaleNumber = "TESTE-S20",
                SaleDate   = DateTime.Now,
                CustomerId = customerId,
                Customer   = customer,
                Items = new List<SaleItem>
                {
                    new SaleItem
                    {
                        Id          = Guid.NewGuid(),
                        TenantId    = tenantId,
                        ProductId   = produto.Id,
                        Product     = produto,
                        ProductName = produto.Name,
                        Quantity    = 1,
                        UnitPrice   = 10m,
                        TotalItem   = 10m
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

    [Fact(DisplayName = "Cliente identificado por CPF sem IE recebe indicador Não Contribuinte (S20)")]
    public async Task ClienteComCpf_RecebeIndicadorNaoContribuinte()
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();
        var (service, nfceMock, scope) = Build(tenantId, vendaId, Guid.NewGuid(), "10087600994");
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFCE");

            nfceMock.Verify(s => s.EmitirNfceAsync(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.IndicadorIeDestinatario == "9"),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "Venda sem cliente identificado não manda indicador (consumidor não identificado) (S20)")]
    public async Task VendaSemCliente_NaoMandaIndicador()
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();
        var (service, nfceMock, scope) = Build(tenantId, vendaId, customerId: null, customerDocument: null);
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFCE");

            nfceMock.Verify(s => s.EmitirNfceAsync(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req => req.IndicadorIeDestinatario == null && req.CpfCnpj == null),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }

    [Fact(DisplayName = "DataEmissao enviada pra Focus está no horário do Brasil, não 3h no futuro (S21)")]
    public async Task DataEmissao_EstaCorretaNoFusoDoBrasil()
    {
        var tenantId = Guid.NewGuid();
        var vendaId  = Guid.NewGuid();
        var antes = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();

        var (service, nfceMock, scope) = Build(tenantId, vendaId, Guid.NewGuid(), "10087600994");
        using (scope)
        {
            await service.EmitirNotaAsync(vendaId, "NFCE");

            var depois = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();

            nfceMock.Verify(s => s.EmitirNfceAsync(
                It.IsAny<string>(),
                It.Is<FocusNfceRequest>(req =>
                    req.DataEmissao != null &&
                    req.DataEmissao.EndsWith("-03:00") &&
                    DateTime.Parse(req.DataEmissao.Substring(0, 19)) >= antes.AddSeconds(-2) &&
                    DateTime.Parse(req.DataEmissao.Substring(0, 19)) <= depois.AddSeconds(2)),
                It.IsAny<string>(),
                It.IsAny<bool>()),
                Times.Once);
        }
    }
}