using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// Trava de ambiente, Estagio 1: a regra pura. O QUE PROVA: a tabela-verdade completa, que so ha autorizacao quando origem, ambiente da
/// chamada e configuracao lida agora sao o MESMO ambiente, e que tudo o mais (divergente, desconhecido, enum nao inicializado, registro
/// inconsistente) falha fechado. O QUE NAO PROVA: nada sobre os workers, o banco ou a Focus; isso e dos Estagios 2 e 3.
/// </summary>
public class PendenciaAmbienteGuardTests
{
    private static readonly bool?[] Origens = { null, true, false };
    private static readonly bool[] Booleanos = { true, false };

    // ── Os quatro casos pedidos (2 argumentos: chamada e configuracao sao o mesmo valor) ─────────────

    [Theory(DisplayName = "Tabela-verdade (ambiente atual unico): so e compativel com origem conhecida e IGUAL ao ambiente atual")]
    [InlineData(false, true, ResultadoAmbiente.Divergente)]    // homologacao -> producao
    [InlineData(true, false, ResultadoAmbiente.Divergente)]    // producao -> homologacao
    [InlineData(true, true, ResultadoAmbiente.Compativel)]
    [InlineData(false, false, ResultadoAmbiente.Compativel)]
    [InlineData(null, true, ResultadoAmbiente.Desconhecido)]   // legado, ambiente atual producao
    [InlineData(null, false, ResultadoAmbiente.Desconhecido)]  // legado, ambiente atual homologacao
    public void TabelaVerdade_AmbienteAtualUnico(bool? criada, bool atual, ResultadoAmbiente esperado)
    {
        var avaliacao = PendenciaAmbienteGuard.Avaliar(criada, atual);

        avaliacao.Resultado.Should().Be(esperado);
        avaliacao.PodeProsseguir.Should().Be(esperado == ResultadoAmbiente.Compativel);
    }

    // ── Tres valores ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Tres valores: das 12 combinacoes, SO duas autorizam (tudo producao ou tudo homologacao); origem nula nunca autoriza")]
    public void TresValores_SoDuasCombinacoesAutorizam()
    {
        var autorizadas = new List<(bool? Criada, bool Chamada, bool Configurado)>();

        foreach (var criada in Origens)
        foreach (var chamada in Booleanos)
        foreach (var configurado in Booleanos)
        {
            var avaliacao = PendenciaAmbienteGuard.Avaliar(criada, chamada, configurado);

            if (avaliacao.PodeProsseguir)
                autorizadas.Add((criada, chamada, configurado));

            if (criada is null)
                avaliacao.Resultado.Should().Be(ResultadoAmbiente.Desconhecido);
        }

        autorizadas.Should().BeEquivalentTo(new (bool?, bool, bool)[] { (true, true, true), (false, false, false) });
    }

    [Fact(DisplayName = "CONFIGURACAO MUDA ENTRE A VALIDACAO E O ENVIO: homologacao validada no inicio, configuracao relida como producao: BLOQUEIA")]
    public void ConfiguracaoMudaEntreValidacaoEEnvio_HomologacaoParaProducao_Bloqueia()
    {
        var noInicio = PendenciaAmbienteGuard.Avaliar(false, ambienteAtualProducao: false);
        noInicio.PodeProsseguir.Should().BeTrue("a validacao inicial e compativel");

        var noEnvio = PendenciaAmbienteGuard.Avaliar(
            false, ambienteDaChamadaProducao: false, ambienteConfiguradoAgoraProducao: true);

        noEnvio.Resultado.Should().Be(ResultadoAmbiente.Divergente);
        noEnvio.PodeProsseguir.Should().BeFalse("a compatibilidade verificada no inicio NAO e autorizacao permanente");
        noEnvio.Descricao.Should().Contain("configurado agora: Producao");
    }

    [Fact(DisplayName = "CONFIGURACAO MUDA ENTRE A VALIDACAO E O ENVIO: producao validada no inicio, configuracao relida como homologacao: BLOQUEIA")]
    public void ConfiguracaoMudaEntreValidacaoEEnvio_ProducaoParaHomologacao_Bloqueia()
    {
        PendenciaAmbienteGuard.Avaliar(true, ambienteAtualProducao: true).PodeProsseguir.Should().BeTrue();

        var noEnvio = PendenciaAmbienteGuard.Avaliar(
            true, ambienteDaChamadaProducao: true, ambienteConfiguradoAgoraProducao: false);

        noEnvio.Resultado.Should().Be(ResultadoAmbiente.Divergente);
        noEnvio.PodeProsseguir.Should().BeFalse();
    }

    [Fact(DisplayName = "CONTEXTO VELHO: pendencia de homologacao, configuracao relida como homologacao, mas a chamada usaria PRODUCAO (token e host velhos): BLOQUEIA")]
    public void ContextoDesatualizado_ConfiguracaoBateComAPendenciaMasAChamadaNao_Bloqueia()
    {
        // O caso que uma comparacao de dois valores deixaria passar: pendencia == configuracao atual, mas o contexto montado no
        // inicio do ciclo (que decide host e token) ainda aponta para o outro ambiente.
        var avaliacao = PendenciaAmbienteGuard.Avaliar(
            false, ambienteDaChamadaProducao: true, ambienteConfiguradoAgoraProducao: false);

        avaliacao.Resultado.Should().Be(ResultadoAmbiente.Divergente);
        avaliacao.PodeProsseguir.Should().BeFalse();
        avaliacao.Descricao.Should().Contain("a chamada usaria Producao");
    }

    // ── Falha fechada ────────────────────────────────────────────────────

    [Fact(DisplayName = "O enum nao inicializado NAO e 'compativel' e nenhum valor indefinido autoriza")]
    public void EnumNaoInicializado_NuncaAutoriza()
    {
        default(ResultadoAmbiente).Should().NotBe(ResultadoAmbiente.Compativel);
        ((int)ResultadoAmbiente.Compativel).Should().BeGreaterThan(0);

        foreach (var indefinido in new[] { 0, 5, 99, -1 })
        {
            new AvaliacaoAmbiente((ResultadoAmbiente)indefinido, true, true, true).PodeProsseguir
                .Should().BeFalse($"o valor {indefinido} nao e 'compativel'");
        }
    }

    [Fact(DisplayName = "Um registro rotulado 'Compativel' mas INCONSISTENTE com os dados (ou sem origem) tambem nao autoriza")]
    public void RegistroInconsistente_NaoAutoriza()
    {
        new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, true, false, false).PodeProsseguir.Should().BeFalse();
        new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, false, false, true).PodeProsseguir.Should().BeFalse();
        new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, true, true, false).PodeProsseguir.Should().BeFalse();
        new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, null, true, true).PodeProsseguir.Should().BeFalse();
        new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, null, false, false).PodeProsseguir.Should().BeFalse();
    }

    // ── Textos e pureza ──────────────────────────────────────────────────

    [Fact(DisplayName = "Nomes e descricao: sem acentos, citam a origem, o ambiente da chamada e o configurado agora")]
    public void Descricao_CitaOsTresValores_SemAcentos()
    {
        foreach (var criada in Origens)
        foreach (var chamada in Booleanos)
        foreach (var configurado in Booleanos)
        {
            var avaliacao = PendenciaAmbienteGuard.Avaliar(criada, chamada, configurado);

            avaliacao.Descricao.Should().Contain($"a chamada usaria {(chamada ? "Producao" : "Homologacao")}");
            avaliacao.Descricao.Should().Contain($"configurado agora: {(configurado ? "Producao" : "Homologacao")}");
            avaliacao.Descricao.All(c => c < 128).Should().BeTrue("o texto vai para o log e para UltimaDecisao (ASCII)");

            if (criada is null)
                avaliacao.Descricao.Should().Contain("sem ambiente de origem registrado");
            else
                avaliacao.Descricao.Should().Contain($"pendencia criada em {(criada.Value ? "Producao" : "Homologacao")}");
        }
    }

    [Fact(DisplayName = "A guarda e pura: as mesmas entradas dao sempre o mesmo resultado")]
    public void Pura_MesmaEntradaMesmoResultado()
    {
        foreach (var criada in Origens)
        foreach (var chamada in Booleanos)
        foreach (var configurado in Booleanos)
        {
            PendenciaAmbienteGuard.Avaliar(criada, chamada, configurado)
                .Should().Be(PendenciaAmbienteGuard.Avaliar(criada, chamada, configurado));
        }
    }

    // ── Configuracao ilegivel (Estagio 2) ────────────────────────────────

    [Fact(DisplayName = "Configuracao ILEGIVEL: nunca autoriza, para qualquer origem e ambiente de chamada (falha fechada)")]
    public void ConfiguracaoIlegivel_NuncaAutoriza()
    {
        foreach (var criada in Origens)
        foreach (var chamada in Booleanos)
        {
            var avaliacao = PendenciaAmbienteGuard.ConfiguracaoIlegivel(criada, chamada);

            avaliacao.Resultado.Should().Be(ResultadoAmbiente.Indeterminado);
            avaliacao.ConfiguracaoLegivel.Should().BeFalse();
            avaliacao.PodeProsseguir.Should().BeFalse();
            avaliacao.Descricao.Should().Contain("nao foi possivel ler a configuracao fiscal atual");
            avaliacao.Descricao.All(c => c < 128).Should().BeTrue();
        }
    }

    [Fact(DisplayName = "Um registro rotulado 'Compativel' mas marcado como ILEGIVEL tambem nao autoriza; Indeterminado e um valor definido (4)")]
    public void RegistroCompativelMasIlegivel_NaoAutoriza()
    {
        new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, true, true, true) { ConfiguracaoLegivel = false }
            .PodeProsseguir.Should().BeFalse();

        ((int)ResultadoAmbiente.Indeterminado).Should().Be(4);
        ResultadoAmbiente.Indeterminado.Should().NotBe(ResultadoAmbiente.Compativel);
    }
}
