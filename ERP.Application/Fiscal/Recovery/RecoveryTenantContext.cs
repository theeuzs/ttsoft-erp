namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// O que o orquestrador precisa saber do tenant do ciclo. Quem resolve o tenant e a
/// configuracao fiscal (o worker, 4A-6) passa isto pronto: o orquestrador nao le configuracao.
/// </summary>
public sealed record RecoveryTenantContext(string Token, bool IsProducao)
{
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
}
