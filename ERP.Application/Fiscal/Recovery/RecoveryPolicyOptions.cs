namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Parametros da politica de recuperacao. Valores padrao = decisoes aprovadas
/// (07/10/2026). Todas as datas/horas da politica sao DateTimeOffset (UTC).
/// </summary>
public sealed record RecoveryPolicyOptions
{
    /// <summary>
    /// Espera por numero de falhas TRANSITORIAS seguidas (1a, 2a, 3a...). Depois
    /// do ultimo item, repete o ultimo (teto). 2, 2, 4, 8, 15, 30, 30...
    /// </summary>
    public IReadOnlyList<TimeSpan> Backoff { get; init; } = new[]
    {
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30)
    };

    /// <summary>Intervalo entre consultas enquanto a Focus diz que ainda esta processando.</summary>
    public TimeSpan EsperaProcessando { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Quanto tempo (desde que entrou na fila) uma pendencia pode ficar so "processando".</summary>
    public TimeSpan EsperaMaximaProcessando { get; init; } = TimeSpan.FromHours(6);

    /// <summary>Intervalo de retentativa para um resultado desconhecido (ate o limite).</summary>
    public TimeSpan EsperaDesconhecido { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Quantos resultados desconhecidos SEGUIDOS levam a intervencao manual (K).</summary>
    public int LimiteDesconhecidos { get; init; } = 3;

    /// <summary>Intervalo de consulta enquanto aguarda correcao de credencial (401/403).</summary>
    public TimeSpan EsperaAguardandoCorrecao { get; init; } = TimeSpan.FromMinutes(30);

    // ── D8: regeneracao automatica de DataEmissao ───────────────────────────
    // Regra fiscal, nao tecnica: so liga depois que o contador aprovar N.

    /// <summary>Interruptor da D8. PADRAO DESLIGADO.</summary>
    public bool PermitirRegeneracaoDataEmissao { get; init; } = false;

    /// <summary>Idade maxima (horas) do DataEmissao do payload para regenerar. PADRAO SEM VALOR (N pendente do contador).</summary>
    public double? JanelaMaximaRegeneracaoHoras { get; init; } = null;
}
