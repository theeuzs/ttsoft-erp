using ERP.Api.BackgroundServices;
using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Helpers;
using ERP.Application.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using ERP.Tests.Controllers;
using ERP.Tests.Fiscal;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// 4A-6b: o worker da recuperacao na composicao REAL da aplicacao (Program.cs verdadeiro via ErpApiFactory). A Focus e roteirizada (nada chama a
/// Focus real); todo o resto do grafo e real, inclusive o IFiscalConfigurationProvider do banco (token por tenant) e o filtro de tenant do AppDbContext.
///
/// O QUE PROVA: com dois tenants habilitados e um terceiro nao, o worker define o tenant ANTES de resolver, le o token de CADA tenant e so toca os
/// habilitados. O QUE NAO PROVA: SQL Server (a fabrica usa SQLite), NoTracking (a fabrica rastreia), ambiente de producao, o caminho de autorizacao
/// (exigiria semear venda e itens) nem o ciclo dormir/acordar do plano F1.
/// </summary>
public class NfeRecoveryHostedServiceComposicaoRealTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public NfeRecoveryHostedServiceComposicaoRealTests(ErpApiFactory factory)
    {
        _factory = factory;
    }

    private static (Guid Id, string Referencia) SemearTenant(IServiceProvider servicos, Guid tenantId, string token)
    {
        using var escopo = servicos.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IRequestTenant>().TenantId = tenantId;   // ANTES de resolver o contexto
        var ctx = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        ctx.TenantFiscalConfigurations.Add(new TenantFiscalConfiguration
        {
            TenantId = tenantId,
            TokenFocusNfeProducaoEncriptado = TokenProtector.Proteger("nao-usado"),
            TokenFocusNfeHomologacaoEncriptado = TokenProtector.Proteger(token),
            UsarAmbienteProducao = false
        });

        var nota = new NfePendente
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendaId = Guid.NewGuid(),
            TipoNota = "NFCE",
            PayloadJson = RecoveryFixtures.PayloadNfce,
            Referencia = Guid.NewGuid().ToString(),
            DataFalha = FusoBrasilHelper.AgoraNoBrasil().AddMinutes(-30),
            CriadaEmProducao = false   // a configuracao semeada e homologacao
        };
        ctx.NfePendentes.Add(nota);
        ctx.SaveChanges();

        return (nota.Id, nota.Referencia);
    }

    private static NfePendente Ler(IServiceProvider servicos, Guid tenantId, Guid id)
    {
        using var escopo = servicos.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IRequestTenant>().TenantId = tenantId;
        var ctx = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return ctx.NfePendentes.AsNoTracking().Single(n => n.Id == id);
    }

    [Fact(DisplayName = "COMPOSICAO REAL: o worker define o tenant ANTES de resolver, usa o token de CADA tenant habilitado e nao toca o nao habilitado")]
    public async Task ComposicaoReal_TenantAntesDeResolver_TokenPorTenant_SoHabilitados()
    {
        var eventos = new List<string>();
        var focus = new FocusRoteirizado(eventos);
        using var fabrica = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFocusReferenceClient>();
            services.AddSingleton<IFocusReferenceClient>(focus);
        }));
        var servicos = fabrica.Services;

        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var (idA, refA) = SemearTenant(servicos, a, "token-real-A");
        var (idB, refB) = SemearTenant(servicos, b, "token-real-B");
        var (idC, _) = SemearTenant(servicos, c, "token-real-C");
        focus.EnfileirarGet(Resp.Processando(), Resp.Processando());
        var worker = new NfeRecoveryHostedService(
            fabrica.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NfeRecoveryHostedService>.Instance,
            new FiscalRecoverySwitch($"{a},{b}"),
            new FiscalRecoveryHeartbeat());

        await worker.ExecutarCicloAsync(CancellationToken.None);

        focus.Chamadas.Should().HaveCount(2).And.OnlyContain(x => x.Metodo == "GET");
        focus.Chamadas.Should().Contain(x => x.Referencia == refA && x.Token == "token-real-A");
        focus.Chamadas.Should().Contain(x => x.Referencia == refB && x.Token == "token-real-B");
        Ler(servicos, a, idA).TentativasConsulta.Should().Be(1);
        Ler(servicos, b, idB).TentativasConsulta.Should().Be(1);
        Ler(servicos, c, idC).TentativasConsulta.Should().Be(0, "o tenant C nao esta na lista");
    }

    private static void DefinirAmbiente(IServiceProvider servicos, Guid tenantId, bool producao)
    {
        using var escopo = servicos.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IRequestTenant>().TenantId = tenantId;
        var ctx = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        ctx.TenantFiscalConfigurations
            .Where(c => c.TenantId == tenantId)
            .ExecuteUpdate(s => s.SetProperty(c => c.UsarAmbienteProducao, producao));
    }

    private static void TornarElegivel(IServiceProvider servicos, Guid tenantId, Guid id)
    {
        using var escopo = servicos.CreateScope();
        escopo.ServiceProvider.GetRequiredService<IRequestTenant>().TenantId = tenantId;
        var ctx = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        ctx.NfePendentes
            .Where(n => n.Id == id)
            .ExecuteUpdate(s => s.SetProperty(n => n.ProximaTentativaEm, (DateTime?)null));
    }

    [Fact(DisplayName = "COMPOSICAO REAL, O CENARIO DA NOITE: o flag do tenant e trocado NO BANCO depois de a pendencia nascer: bloqueia sem chamar a Focus e retoma quando o flag volta")]
    public async Task ComposicaoReal_FlagTrocadoNoBanco_BloqueiaERetoma()
    {
        var eventos = new List<string>();
        var focus = new FocusRoteirizado(eventos);
        using var fabrica = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFocusReferenceClient>();
            services.AddSingleton<IFocusReferenceClient>(focus);
        }));
        var servicos = fabrica.Services;
        var tenant = Guid.NewGuid();
        var (id, _) = SemearTenant(servicos, tenant, "token-real-A");   // configuracao e pendencia em HOMOLOGACAO
        var worker = new NfeRecoveryHostedService(
            servicos.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NfeRecoveryHostedService>.Instance,
            new FiscalRecoverySwitch(tenant.ToString()),
            new FiscalRecoveryHeartbeat());

        // 1) Compativel (homologacao): consulta a Focus.
        focus.EnfileirarGet(Resp.Processando());
        await worker.ExecutarCicloAsync(CancellationToken.None);
        focus.Chamadas.Should().ContainSingle();

        // 2) O flag e trocado no banco (producao) e a pendencia volta a ser elegivel: NENHUMA chamada nova.
        DefinirAmbiente(servicos, tenant, producao: true);
        TornarElegivel(servicos, tenant, id);
        await worker.ExecutarCicloAsync(CancellationToken.None);

        focus.Chamadas.Should().ContainSingle("a troca do flag no banco bloqueia a recuperacao");
        var bloqueada = Ler(servicos, tenant, id);
        bloqueada.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        bloqueada.UltimaDecisao.Should().StartWith("AmbienteDivergente:");

        // 3) O flag volta a homologacao: a pendencia retoma.
        DefinirAmbiente(servicos, tenant, producao: false);
        TornarElegivel(servicos, tenant, id);
        focus.EnfileirarGet(Resp.Processando());
        await worker.ExecutarCicloAsync(CancellationToken.None);

        focus.Chamadas.Should().HaveCount(2);
    }
}
