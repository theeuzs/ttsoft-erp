using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Tests.Fiscal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// Trava de ambiente, Estagio 2: a ORIGEM da pendencia e persistida na criacao e NUNCA deduzida do ambiente atual.
/// O QUE PROVA: o servico grava exatamente o que o chamador informa; a emissao em contingencia informa o ambiente da configuracao lida;
/// a coluna guarda verdadeiro, falso e nulo; e o parametro nao tem valor padrao. O QUE NAO PROVA: a coluna no SQL Server (so a migration prova).
/// </summary>
public class NfePendenteCriadaEmProducaoTests
{
    [Theory(DisplayName = "NfeContingencyService grava CriadaEmProducao exatamente como o chamador informa")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Servico_GravaAOrigemInformada(bool emProducao)
    {
        var uow = new Mock<IUnitOfWork>();
        var repo = new Mock<INfePendenteRepository>();
        uow.Setup(u => u.NfePendentes).Returns(repo.Object);
        uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);
        var tenant = new Mock<IRequestTenant>();
        tenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        var servico = new NfeContingencyService(uow.Object, tenant.Object);
        NfePendente? gravada = null;
        repo.Setup(r => r.AddAsync(It.IsAny<NfePendente>())).Callback<NfePendente>(n => gravada = n).Returns(Task.CompletedTask);

        await servico.RegistrarNotaPendenteAsync(Guid.NewGuid(), "NFCE", "{}", emProducao);

        gravada.Should().NotBeNull();
        gravada!.CriadaEmProducao.Should().Be(emProducao);
    }

    [Fact(DisplayName = "O parametro do ambiente e OBRIGATORIO: sem valor padrao, nenhum chamador pode esquecer de informa-lo")]
    public void ParametroDoAmbiente_NaoTemValorPadrao()
    {
        var metodo = typeof(INfeContingencyService).GetMethod(nameof(INfeContingencyService.RegistrarNotaPendenteAsync))!;
        var parametro = metodo.GetParameters().Last();

        parametro.Name.Should().Be("emProducao");
        parametro.ParameterType.Should().Be(typeof(bool));
        parametro.HasDefaultValue.Should().BeFalse();
    }

    [Theory(DisplayName = "A coluna guarda verdadeiro, falso e NULO (desconhecido) sem reinterpretar")]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void Coluna_GuardaOTrilean(bool? origem)
    {
        using var amb = new RecoveryAmbiente();

        var id = amb.Semear(n => n.CriadaEmProducao = origem);

        amb.Ler(id).CriadaEmProducao.Should().Be(origem);
    }

    // ── EmitirNotaAsync: o ambiente informado e o da configuracao que fez o POST falhar ─────────────

    private static (FiscalService Servico, Mock<INfeContingencyService> Contingencia, IServiceScope Escopo) Montar(Guid tenantId, Guid vendaId, bool producao)
    {
        var provider = TestDb.Create(dbName: $"fiscal_criada_{Guid.NewGuid():N}", tenantId: tenantId, seed: ctx =>
        {
            var produto = new Product { Id = Guid.NewGuid(), Name = "Disco de Corte", TenantId = tenantId, SalePrice = 2.99m };
            ctx.Products.Add(produto);
            var cliente = new Customer { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Cliente", Document = "12443095916" };
            ctx.Customers.Add(cliente);
            ctx.Sales.Add(new Sale
            {
                Id = vendaId, TenantId = tenantId, SaleNumber = "TESTE-AMB", SaleDate = DateTime.Now, CustomerId = cliente.Id, Customer = cliente,
                Items = new List<SaleItem>
                {
                    new() { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = produto.Id, Product = produto, ProductName = produto.Name, Quantity = 10, UnitPrice = 2.99m, TotalItem = 29.90m }
                }
            });
        });

        var escopo = provider.CreateScope();
        var ctx0 = escopo.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();

        var config = new Mock<IFiscalConfigurationProvider>();
        config.Setup(c => c.ObterConfiguracaoAsync()).ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = producao });

        var nfe = new Mock<INfeEmissionService>();
        nfe.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((false, "Erro de Comunicação: sem rede", "", "", "", ""));

        var contingencia = new Mock<INfeContingencyService>();
        var servico = new FiscalService(
            ctx0, config.Object, new Mock<INfceEmissionService>().Object, nfe.Object, contingencia.Object,
            new Mock<ISaleService>().Object, new Mock<INfeStatusService>().Object);

        return (servico, contingencia, escopo);
    }

    [Theory(DisplayName = "EmitirNotaAsync em falha de comunicacao registra a pendencia com o ambiente da configuracao lida (producao e homologacao)")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmissaoEmContingencia_InformaOAmbienteDaConfiguracao(bool producao)
    {
        var tenantId = Guid.NewGuid();
        var vendaId = Guid.NewGuid();
        var (servico, contingencia, escopo) = Montar(tenantId, vendaId, producao);
        using (escopo)
        {
            await servico.EmitirNotaAsync(vendaId, "NFE");

            contingencia.Verify(c => c.RegistrarNotaPendenteAsync(vendaId, "NFE", It.IsAny<string>(), producao), Times.Once());
            contingencia.Verify(c => c.RegistrarNotaPendenteAsync(vendaId, "NFE", It.IsAny<string>(), !producao), Times.Never());
        }
    }
}
