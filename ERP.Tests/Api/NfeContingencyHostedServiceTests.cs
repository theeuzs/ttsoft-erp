// ERP.Tests/Api/NfeContingencyHostedServiceTests.cs
using ERP.Api.BackgroundServices;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// S25 (18/08) — item pendente da auditoria de 13/08: NfeContingencyWorker
/// virou IHostedService na API, processando TODOS os tenants (antes só
/// rodava enquanto o WPF de alguma loja estivesse aberto). O ponto mais
/// arriscado dessa mudança é isolamento entre tenants — o AppDbContext da
/// API só copia IRequestTenant.TenantId pro filtro UMA VEZ, na hora que é
/// construído, então a ORDEM de resolução dentro do scope importa (setar
/// o tenant antes de resolver qualquer coisa que dependa do contexto).
///
/// NfeContingencyService.VerificarConexaoSefazAsync() faz um ping de rede
/// de verdade (código pré-existente, fora do escopo desse fix) — por isso
/// esses testes não passam pelo fluxo completo ProcessarTenantAsync (seria
/// um teste instável, dependente de rede). Cobrem as duas partes que
/// realmente decidem se o isolamento funciona: a descoberta de quais
/// tenants têm nota pendente, e o padrão "seta tenant antes de resolver
/// o contexto" que ProcessarTenantAsync segue.
/// </summary>
public class NfeContingencyHostedServiceTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IRequestTenant, ERP.Api.Services.RequestTenant>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "ObterTenantsComNotasPendentesAsync descobre todos os tenants com nota pendente, sem misturar dados")]
    public async Task ObterTenantsComNotasPendentesAsync_DescobreTodosOsTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantSemPendencia = Guid.NewGuid();

        var provider = BuildProvider($"contingencia_{Guid.NewGuid():N}");

        using (var seedScope = provider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.NfePendentes.Add(new NfePendente { Id = Guid.NewGuid(), TenantId = tenantA, VendaId = Guid.NewGuid(), TipoNota = "NFCE", PayloadJson = "{}", Referencia = "ref-a" });
            ctx.NfePendentes.Add(new NfePendente { Id = Guid.NewGuid(), TenantId = tenantB, VendaId = Guid.NewGuid(), TipoNota = "NFCE", PayloadJson = "{}", Referencia = "ref-b" });
            await ctx.SaveChangesAsync();
        }

        var service = new NfeContingencyHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NfeContingencyHostedService>.Instance);

        var tenants = await service.ObterTenantsComNotasPendentesAsync(CancellationToken.None);

        tenants.Should().Contain(tenantA);
        tenants.Should().Contain(tenantB);
        tenants.Should().NotContain(tenantSemPendencia);
        tenants.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Setar IRequestTenant.TenantId ANTES de resolver o AppDbContext isola os dados por tenant (o padrão que ProcessarTenantAsync segue)")]
    public async Task SetarTenantAntesDeResolverContexto_IsolaOsDados()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var provider = BuildProvider($"contingencia_isolamento_{Guid.NewGuid():N}");

        using (var seedScope = provider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.NfePendentes.Add(new NfePendente { Id = Guid.NewGuid(), TenantId = tenantA, VendaId = Guid.NewGuid(), TipoNota = "NFCE", PayloadJson = "{}", Referencia = "ref-a" });
            ctx.NfePendentes.Add(new NfePendente { Id = Guid.NewGuid(), TenantId = tenantB, VendaId = Guid.NewGuid(), TipoNota = "NFCE", PayloadJson = "{}", Referencia = "ref-b" });
            await ctx.SaveChangesAsync();
        }

        // Reproduz exatamente a ordem que ProcessarTenantAsync segue: cria o
        // scope, resolve e seta IRequestTenant PRIMEIRO, só depois resolve o
        // AppDbContext (que copia o TenantId pro filtro no construtor).
        using (var scopeA = provider.CreateScope())
        {
            var requestTenant = scopeA.ServiceProvider.GetRequiredService<IRequestTenant>();
            requestTenant.TenantId = tenantA;

            var ctx = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendentesVisiveis = await ctx.NfePendentes.ToListAsync();

            pendentesVisiveis.Should().ContainSingle(n => n.Referencia == "ref-a");
            pendentesVisiveis.Should().NotContain(n => n.Referencia == "ref-b");
        }

        using (var scopeB = provider.CreateScope())
        {
            var requestTenant = scopeB.ServiceProvider.GetRequiredService<IRequestTenant>();
            requestTenant.TenantId = tenantB;

            var ctx = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendentesVisiveis = await ctx.NfePendentes.ToListAsync();

            pendentesVisiveis.Should().ContainSingle(n => n.Referencia == "ref-b");
            pendentesVisiveis.Should().NotContain(n => n.Referencia == "ref-a");
        }
    }

    [Fact(DisplayName = "Resolver o AppDbContext ANTES de setar o tenant NÃO isola (prova por que a ordem importa)")]
    public async Task ResolverContextoAntesDeSetarTenant_NaoIsola()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var provider = BuildProvider($"contingencia_ordem_errada_{Guid.NewGuid():N}");

        using (var seedScope = provider.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.NfePendentes.Add(new NfePendente { Id = Guid.NewGuid(), TenantId = tenantA, VendaId = Guid.NewGuid(), TipoNota = "NFCE", PayloadJson = "{}", Referencia = "ref-a" });
            await ctx.SaveChangesAsync();
        }

        using var scope = provider.CreateScope();

        // Ordem ERRADA de propósito: resolve o AppDbContext primeiro (o
        // construtor já copia o TenantId — que ainda é Guid.Empty aqui).
        var ctxErrado = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var requestTenant = scope.ServiceProvider.GetRequiredService<IRequestTenant>();
        requestTenant.TenantId = tenantA; // tarde demais — o contexto já foi construído

        var pendentesVisiveis = await ctxErrado.NfePendentes.ToListAsync();

        // Sem tenant válido no momento da construção, o filtro cai pra
        // Guid.Empty — não vê a nota do tenantA (prova que a ordem tem
        // que ser: IRequestTenant primeiro, AppDbContext depois).
        pendentesVisiveis.Should().BeEmpty();
    }
}
