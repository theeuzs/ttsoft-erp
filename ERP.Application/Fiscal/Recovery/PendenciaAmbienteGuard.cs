namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Resultado da guarda de ambiente (trava de ambiente, Estagio 1). O valor 0 NAO existe de proposito: um enum nao inicializado
/// nunca pode ser lido como "compativel".
/// </summary>
public enum ResultadoAmbiente
{
    /// <summary>Origem conhecida e IGUAL ao ambiente da chamada e ao ambiente configurado agora. O UNICO resultado que autoriza uma chamada a Focus.</summary>
    Compativel = 1,

    /// <summary>Origem conhecida, mas diferente do ambiente da chamada e/ou do ambiente configurado agora.</summary>
    Divergente = 2,

    /// <summary>Pendencia sem ambiente de origem registrado (anterior a trava). Falha fechada: nunca e reenviada sozinha.</summary>
    Desconhecido = 3
}

/// <summary>
/// O que a guarda concluiu, com os tres valores comparados. <see cref="PodeProsseguir"/> NAO confia so no rotulo: recalcula a partir
/// dos dados, de modo que um registro montado a mao ou inconsistente tambem nao autoriza nada.
/// </summary>
/// <param name="Resultado">O veredito da guarda.</param>
/// <param name="CriadaEmProducao">Ambiente em que a pendencia nasceu: true = Producao, false = Homologacao, nulo = desconhecido.</param>
/// <param name="AmbienteDaChamadaProducao">Ambiente que a chamada a Focus VAI usar (host e token do contexto).</param>
/// <param name="AmbienteConfiguradoAgoraProducao">Ambiente da configuracao fiscal do tenant, LIDA de novo imediatamente antes.</param>
public sealed record AvaliacaoAmbiente(
    ResultadoAmbiente Resultado,
    bool? CriadaEmProducao,
    bool AmbienteDaChamadaProducao,
    bool AmbienteConfiguradoAgoraProducao)
{
    /// <summary>True SOMENTE se o rotulo e Compativel E os tres valores realmente coincidem (origem conhecida).</summary>
    public bool PodeProsseguir =>
        Resultado == ResultadoAmbiente.Compativel
        && CriadaEmProducao.HasValue
        && CriadaEmProducao.Value == AmbienteDaChamadaProducao
        && CriadaEmProducao.Value == AmbienteConfiguradoAgoraProducao;

    public string NomeDaOrigem => CriadaEmProducao switch
    {
        true => "Producao",
        false => "Homologacao",
        null => "desconhecido"
    };

    public string NomeDaChamada => Nome(AmbienteDaChamadaProducao);

    public string NomeConfigurado => Nome(AmbienteConfiguradoAgoraProducao);

    /// <summary>Texto curto e SEM acentos, para log e para UltimaDecisao.</summary>
    public string Descricao => CriadaEmProducao is null
        ? $"pendencia sem ambiente de origem registrado; a chamada usaria {NomeDaChamada}; configurado agora: {NomeConfigurado}"
        : $"pendencia criada em {NomeDaOrigem}; a chamada usaria {NomeDaChamada}; configurado agora: {NomeConfigurado}";

    private static string Nome(bool producao) => producao ? "Producao" : "Homologacao";
}

/// <summary>
/// Regra UNICA da trava de ambiente: uma pendencia nascida em um ambiente da Focus nunca pode ser consultada nem reenviada em outro.
/// Funcao pura (sem I/O): os dois workers e o orquestrador chamam esta mesma regra; nenhum compara ambientes por conta propria.
///
/// A comparacao e de TRES valores, e nao de dois. Em um ciclo longo a configuracao do tenant pode mudar depois de o contexto
/// (token e host) ja ter sido montado; comparar so "pendencia x configuracao" deixaria passar uma pendencia de homologacao com contexto
/// montado para producao. Por isso a chamada so e autorizada quando a ORIGEM da pendencia, o AMBIENTE QUE A CHAMADA VAI USAR e a
/// CONFIGURACAO LIDA AGORA sao o mesmo ambiente.
/// </summary>
public static class PendenciaAmbienteGuard
{
    public static AvaliacaoAmbiente Avaliar(
        bool? criadaEmProducao, bool ambienteDaChamadaProducao, bool ambienteConfiguradoAgoraProducao)
    {
        ResultadoAmbiente resultado;

        if (criadaEmProducao is null)
            resultado = ResultadoAmbiente.Desconhecido;
        else if (criadaEmProducao.Value == ambienteDaChamadaProducao && criadaEmProducao.Value == ambienteConfiguradoAgoraProducao)
            resultado = ResultadoAmbiente.Compativel;
        else
            resultado = ResultadoAmbiente.Divergente;

        return new AvaliacaoAmbiente(resultado, criadaEmProducao, ambienteDaChamadaProducao, ambienteConfiguradoAgoraProducao);
    }

    /// <summary>Atalho para quando o ambiente da chamada e o configurado agora sao o mesmo valor (ex.: verificacao no inicio do ciclo).</summary>
    public static AvaliacaoAmbiente Avaliar(bool? criadaEmProducao, bool ambienteAtualProducao) =>
        Avaliar(criadaEmProducao, ambienteAtualProducao, ambienteAtualProducao);
}
