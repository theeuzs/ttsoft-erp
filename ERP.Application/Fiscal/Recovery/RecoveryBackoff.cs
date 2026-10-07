namespace ERP.Application.Fiscal.Recovery;

/// <summary>Backoff progressivo com teto. Funcao pura.</summary>
public static class RecoveryBackoff
{
    /// <summary>
    /// Espera para a n-esima falha transitoria seguida (n comeca em 1). Se n
    /// passa do tamanho da sequencia, repete o ultimo valor (teto).
    /// </summary>
    public static TimeSpan Espera(int falhasSeguidas, IReadOnlyList<TimeSpan> sequencia)
    {
        ArgumentNullException.ThrowIfNull(sequencia);

        if (sequencia.Count == 0)
            throw new ArgumentException("A sequencia de backoff nao pode ser vazia.", nameof(sequencia));

        if (falhasSeguidas < 1)
            throw new ArgumentOutOfRangeException(nameof(falhasSeguidas), falhasSeguidas, "Deve ser >= 1.");

        var indice = Math.Min(falhasSeguidas, sequencia.Count) - 1;
        return sequencia[indice];
    }
}
