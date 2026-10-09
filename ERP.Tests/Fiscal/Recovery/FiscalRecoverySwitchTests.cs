using ERP.Api.Extensions;
using ERP.Application.Fiscal.Recovery;
using ERP.Tests.Controllers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-6a: o interruptor da recuperacao fiscal por tenant. FALHA FECHADA: configuracao ausente, vazia ou INVALIDA
/// significa nenhum tenant habilitado; nunca se habilita "o que deu para entender" de uma lista quebrada.
/// </summary>
public class FiscalRecoverySwitchTests
{
    // Com letras hexadecimais de proposito: so assim o teste de maiusculas/minusculas prova alguma coisa.
    private static readonly Guid A = Guid.Parse("a1b2c3d4-0000-4000-8000-00000000000a");
    private static readonly Guid B = Guid.Parse("b2c3d4e5-0000-4000-8000-00000000000b");
    private static readonly Guid Outro = Guid.Parse("ffffffff-0000-4000-8000-00000000000f");

    /// <summary>Troca os marcadores pelos GUIDs de teste, inclusive em formatos que o interruptor NAO aceita.</summary>
    private static string Montar(string modelo) => modelo
        .Replace("<AN>", A.ToString("N"))
        .Replace("<AB>", A.ToString("B"))
        .Replace("<AP>", A.ToString("P"))
        .Replace("<A>", A.ToString())
        .Replace("<B>", B.ToString());

    // ── Configuracao ausente/vazia: valida, ninguem habilitado ───────────

    [Theory(DisplayName = "Configuracao ausente, vazia ou em branco: e VALIDA e nenhum tenant fica habilitado")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Ausente_NinguemHabilitado(string? valor)
    {
        var interruptor = new FiscalRecoverySwitch(valor);

        interruptor.ConfiguracaoValida.Should().BeTrue();
        interruptor.MotivoDaInvalidez.Should().BeNull();
        interruptor.TenantsHabilitados.Should().BeEmpty();
        interruptor.EstaHabilitadoPara(A).Should().BeFalse();
        interruptor.EstaHabilitadoPara(Guid.Empty).Should().BeFalse();
    }

    // ── Configuracao valida ──────────────────────────────────────────────

    [Fact(DisplayName = "Um GUID valido habilita SO aquele tenant")]
    public void UmTenant_SoEle()
    {
        var interruptor = new FiscalRecoverySwitch(Montar("<A>"));

        interruptor.ConfiguracaoValida.Should().BeTrue();
        interruptor.EstaHabilitadoPara(A).Should().BeTrue();
        interruptor.EstaHabilitadoPara(B).Should().BeFalse();
        interruptor.EstaHabilitadoPara(Outro).Should().BeFalse();
        interruptor.EstaHabilitadoPara(Guid.Empty).Should().BeFalse();
    }

    [Fact(DisplayName = "Varios GUIDs, com espacos ao redor e em maiusculas ou minusculas: todos habilitados")]
    public void Varios_ComEspacosECaixaMista()
    {
        var interruptor = new FiscalRecoverySwitch($"  {A.ToString().ToUpperInvariant()} ,  {B.ToString().ToLowerInvariant()}  ");

        interruptor.ConfiguracaoValida.Should().BeTrue();
        interruptor.EstaHabilitadoPara(A).Should().BeTrue();
        interruptor.EstaHabilitadoPara(B).Should().BeTrue();
        interruptor.EstaHabilitadoPara(Outro).Should().BeFalse();
        interruptor.TenantsHabilitados.Should().HaveCount(2);
    }

    [Fact(DisplayName = "GUID repetido na lista nao e erro: conta uma vez")]
    public void Repetido_ContaUmaVez()
    {
        var interruptor = new FiscalRecoverySwitch(Montar("<A>,<A>"));

        interruptor.ConfiguracaoValida.Should().BeTrue();
        interruptor.TenantsHabilitados.Should().ContainSingle().Which.Should().Be(A);
    }

    // ── Configuracao invalida: FALHA FECHADA, a lista INTEIRA e rejeitada ─

    [Theory(DisplayName = "Configuracao INVALIDA: nenhum tenant habilitado, nem os itens validos da mesma lista (falha fechada)")]
    [InlineData("lixo")]
    [InlineData("<A>,lixo")]
    [InlineData("lixo,<A>")]
    [InlineData("<A>,")]
    [InlineData(",<A>")]
    [InlineData("<A>,,<B>")]
    [InlineData("<A>;<B>")]
    [InlineData("<A> <B>")]
    [InlineData("*")]
    [InlineData("todos")]
    [InlineData("<AN>")]
    [InlineData("<AB>")]
    [InlineData("<AP>")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("<A>,00000000-0000-0000-0000-000000000000")]
    public void Invalida_NaoHabilitaNinguem(string modelo)
    {
        var interruptor = new FiscalRecoverySwitch(Montar(modelo));

        interruptor.ConfiguracaoValida.Should().BeFalse();
        interruptor.MotivoDaInvalidez.Should().NotBeNullOrWhiteSpace();
        interruptor.TenantsHabilitados.Should().BeEmpty();
        interruptor.EstaHabilitadoPara(A).Should().BeFalse("um item invalido na lista derruba a lista INTEIRA, inclusive o GUID valido");
        interruptor.EstaHabilitadoPara(B).Should().BeFalse();
        interruptor.EstaHabilitadoPara(Guid.Empty).Should().BeFalse();
    }

    [Fact(DisplayName = "O motivo da invalidez aponta o item e nunca despeja um valor gigante no log")]
    public void Invalida_MotivoApontaOItem_ETrunca()
    {
        var interruptor = new FiscalRecoverySwitch(Montar("<A>,") + new string('x', 500));

        interruptor.ConfiguracaoValida.Should().BeFalse();
        interruptor.MotivoDaInvalidez.Should().Contain("item 2");
        interruptor.MotivoDaInvalidez!.Length.Should().BeLessThan(200);
    }

    // ── Registro no DI (o mesmo AddFiscalRecovery do Program.cs) ─────────

    private static ServiceProvider ProvedorCom(string? valorConfigurado)
    {
        var dados = new Dictionary<string, string?>();
        if (valorConfigurado is not null)
            dados[FiscalRecoverySwitch.ChaveDeConfiguracao] = valorConfigurado;

        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(dados).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuracao);
        services.AddFiscalRecovery();
        return services.BuildServiceProvider();
    }

    [Fact(DisplayName = "A chave de configuracao e FiscalRecovery:TenantsHabilitados (App Setting FiscalRecovery__TenantsHabilitados)")]
    public void ChaveDeConfiguracao()
    {
        FiscalRecoverySwitch.ChaveDeConfiguracao.Should().Be("FiscalRecovery:TenantsHabilitados");
    }

    [Fact(DisplayName = "Registro: sem a chave na configuracao, o interruptor resolve valido e vazio")]
    public void Registro_SemChave_Vazio()
    {
        using var provedor = ProvedorCom(null);

        var interruptor = provedor.GetRequiredService<IFiscalRecoverySwitch>();

        interruptor.EstaHabilitadoPara(A).Should().BeFalse();
        ((FiscalRecoverySwitch)interruptor).ConfiguracaoValida.Should().BeTrue();
    }

    [Fact(DisplayName = "Registro: com a chave preenchida, habilita exatamente aqueles tenants")]
    public void Registro_ComChave_Habilita()
    {
        using var provedor = ProvedorCom(Montar("<A>"));

        var interruptor = provedor.GetRequiredService<IFiscalRecoverySwitch>();

        interruptor.EstaHabilitadoPara(A).Should().BeTrue();
        interruptor.EstaHabilitadoPara(B).Should().BeFalse();
    }

    [Fact(DisplayName = "Registro: com a chave INVALIDA, nenhum tenant habilitado (falha fechada tambem pelo DI)")]
    public void Registro_ChaveInvalida_NinguemHabilitado()
    {
        using var provedor = ProvedorCom(Montar("<A>,lixo"));

        var interruptor = provedor.GetRequiredService<IFiscalRecoverySwitch>();

        interruptor.EstaHabilitadoPara(A).Should().BeFalse();
        ((FiscalRecoverySwitch)interruptor).ConfiguracaoValida.Should().BeFalse();
    }

    [Fact(DisplayName = "Registro: o interruptor e singleton (uma unica fonte da verdade para os dois workers)")]
    public void Registro_Singleton()
    {
        var services = new ServiceCollection();
        services.AddFiscalRecovery();

        services.Single(d => d.ServiceType == typeof(IFiscalRecoverySwitch)).Lifetime.Should().Be(ServiceLifetime.Singleton);

        using var provedor = ProvedorCom(Montar("<A>"));
        using var escopo1 = provedor.CreateScope();
        using var escopo2 = provedor.CreateScope();
        escopo1.ServiceProvider.GetRequiredService<IFiscalRecoverySwitch>()
            .Should().BeSameAs(escopo2.ServiceProvider.GetRequiredService<IFiscalRecoverySwitch>());
    }
}

/// <summary>
/// 4A-6a: na composicao REAL da aplicacao (Program.cs verdadeiro via ErpApiFactory), sem a App Setting, o interruptor
/// resolve valido e NENHUM tenant esta habilitado. Se este teste falhar numa maquina, confira se a variavel de ambiente
/// FiscalRecovery__TenantsHabilitados esta definida nela: e exatamente o que ele existe para pegar.
/// </summary>
public class FiscalRecoverySwitchComposicaoRealTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public FiscalRecoverySwitchComposicaoRealTests(ErpApiFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "COMPOSICAO REAL: sem configuracao, o interruptor da aplicacao e valido e nenhum tenant esta habilitado")]
    public void SemConfiguracao_NinguemHabilitado()
    {
        var interruptor = _factory.Services.GetRequiredService<IFiscalRecoverySwitch>();

        interruptor.Should().BeOfType<FiscalRecoverySwitch>();
        var real = (FiscalRecoverySwitch)interruptor;
        real.ConfiguracaoValida.Should().BeTrue();
        real.TenantsHabilitados.Should().BeEmpty("a recuperacao so e habilitada por App Setting explicito");
        real.EstaHabilitadoPara(ErpApiFactory.TestTenantId).Should().BeFalse();
    }
}
