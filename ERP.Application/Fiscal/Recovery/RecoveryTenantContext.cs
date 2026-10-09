namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// O que o orquestrador precisa saber do tenant do ciclo. Quem resolve o tenant e a configuracao fiscal (o worker)
/// passa isto pronto; o orquestrador nao conhece o provider da configuracao, so chama o leitor abaixo.
/// </summary>
/// <param name="Token">Token da Focus do ambiente da chamada.</param>
/// <param name="IsProducao">Ambiente que as chamadas deste ciclo VAO usar (host e token): foi montado no inicio do ciclo.</param>
/// <param name="LerAmbienteConfiguradoAgoraAsync">
/// Trava de ambiente: RELE a configuracao fiscal do tenant NO MOMENTO da chamada e devolve se o ambiente configurado agora e producao.
/// OBRIGATORIO (nao existe contexto sem ele). O orquestrador o chama imediatamente antes de CADA GET e de CADA POST, e nunca reutiliza
/// o resultado de uma chamada anterior. Se lancar, a chamada NAO e feita (falha fechada).
/// </param>
public sealed record RecoveryTenantContext(
    string Token,
    bool IsProducao,
    Func<CancellationToken, Task<bool>> LerAmbienteConfiguradoAgoraAsync)
{
    public Func<CancellationToken, Task<bool>> LerAmbienteConfiguradoAgoraAsync { get; init; } =
        LerAmbienteConfiguradoAgoraAsync ?? throw new ArgumentNullException(nameof(LerAmbienteConfiguradoAgoraAsync));

    /// <summary>Mesmo texto que o NfeContingencyHostedService grava em Sale.NfceAmbiente.</summary>
    public string AmbienteNome => IsProducao ? "Produção" : "Homologação";
}

/// <summary>Contagens de um ciclo, para o worker logar e para os testes.</summary>
public sealed class RecoveryCycleResult
{
    public int Elegiveis { get; set; }

    /// <summary>Autorizacao persistida e pendencia removida.</summary>
    public int Autorizadas { get; set; }

    /// <summary>Rejeicao definitiva: venda marcada Rejeitada e pendencia removida.</summary>
    public int Rejeitadas { get; set; }

    /// <summary>Gravadas com nova agenda (Aguardar / backoff).</summary>
    public int Agendadas { get; set; }

    public int AguardandoCorrecao { get; set; }

    public int IntervencaoManual { get; set; }

    /// <summary>A store recusou a gravacao (nao processavel: ex.: ja em IntervencaoManual).</summary>
    public int Ignoradas { get; set; }

    /// <summary>Excecao inesperada de PROCESSAMENTO, tratada como resultado desconhecido (conta para o limite K).</summary>
    public int ErrosDeProcessamento { get; set; }

    /// <summary>
    /// Falha ao GRAVAR (store, persistencia da autorizacao, venda). NAO e tratada como resposta
    /// desconhecida da Focus e NAO conta para o limite K: nada foi dado como concluido.
    /// </summary>
    public int FalhasDePersistencia { get; set; }

    /// <summary>
    /// Trava de ambiente: a pendencia NAO foi consultada nem reenviada porque o ambiente de origem e divergente, desconhecido ou a
    /// configuracao nao pode ser lida. Nenhuma chamada a Focus foi feita; foi para AguardandoCorrecao.
    /// </summary>
    public int AmbienteDivergente { get; set; }
}
