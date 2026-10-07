namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Estados persistidos da pendencia na fila de recuperacao fiscal (Etapa 4).
/// Texto com constantes, igual a NotaFiscal.Status e Sale.NfceStatusFocus
/// (convencao do dominio fiscal; legivel direto no SSMS).
///
/// So tres estados persistidos. Retry, espera de processamento, autorizada,
/// rejeitada etc. nao sao estados: sao agendamento (ProximaTentativaEm) ou
/// remocao da linha, como ja e hoje.
/// </summary>
public static class NfePendenteEstados
{
    /// <summary>Estado normal: elegivel quando ProximaTentativaEm for nulo ou ja passou.</summary>
    public const string Ativa = "Ativa";

    /// <summary>401/403: problema de credencial, nao da nota. Consulta espacada; volta a Ativa sozinha.</summary>
    public const string AguardandoCorrecao = "AguardandoCorrecao";

    /// <summary>Terminal: so um humano tira daqui. A politica nunca sai deste estado sozinha.</summary>
    public const string IntervencaoManual = "IntervencaoManual";

    public static bool EhValido(string? estado) =>
        estado is Ativa or AguardandoCorrecao or IntervencaoManual;
}
