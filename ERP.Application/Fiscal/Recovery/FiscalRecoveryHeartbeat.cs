namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Batimento do worker da recuperacao fiscal (4A-6b, K4): o worker registra um sinal de vida a CADA ciclo (inclusive com a lista de tenants
/// vazia), e o worker antigo consulta isto para saber se vale um aviso quando deixa NFC-e de fora "para a recuperacao". Sem batimento
/// recente, essas notas podem nao ter ninguem cuidando delas.
/// </summary>
public interface IFiscalRecoveryHeartbeat
{
    /// <summary>Chamado pelo worker da recuperacao a cada ciclo.</summary>
    void RegistrarCiclo();

    /// <summary>
    /// True se o worker bateu dentro da <paramref name="janela"/>, OU se o batimento acabou de ser criado (periodo de
    /// <paramref name="carenciaAposInicio"/>, para nao alertar logo depois de uma subida ou deploy).
    /// </summary>
    bool HaBatimentoRecente(TimeSpan janela, TimeSpan carenciaAposInicio);
}

/// <summary>
/// Implementacao em memoria (singleton). Estado so em memoria e de proposito: um reinicio zera o batimento e abre uma nova carencia,
/// que e exatamente o que se quer depois de uma subida.
/// </summary>
public sealed class FiscalRecoveryHeartbeat : IFiscalRecoveryHeartbeat
{
    private readonly Func<DateTimeOffset> _relogio;
    private readonly DateTimeOffset _criadoEm;
    private long _ultimoCicloTicks;   // 0 = nunca bateu
    private long _totalDeCiclos;

    public FiscalRecoveryHeartbeat(Func<DateTimeOffset>? relogio = null)
    {
        _relogio = relogio ?? (() => DateTimeOffset.UtcNow);
        _criadoEm = _relogio();
    }

    public long TotalDeCiclos => Interlocked.Read(ref _totalDeCiclos);

    public DateTimeOffset? UltimoCiclo
    {
        get
        {
            var ticks = Interlocked.Read(ref _ultimoCicloTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public void RegistrarCiclo()
    {
        Interlocked.Exchange(ref _ultimoCicloTicks, _relogio().UtcTicks);
        Interlocked.Increment(ref _totalDeCiclos);
    }

    public bool HaBatimentoRecente(TimeSpan janela, TimeSpan carenciaAposInicio)
    {
        var agora = _relogio();

        if (agora - _criadoEm < carenciaAposInicio)
            return true;

        var ultimo = UltimoCiclo;
        return ultimo is not null && agora - ultimo.Value <= janela;
    }
}
