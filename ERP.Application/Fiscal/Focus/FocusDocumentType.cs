namespace ERP.Application.Fiscal.Focus;

/// <summary>
/// Tipo de documento fiscal na Focus. Decide o endpoint de consulta.
/// Etapa 4 (A1 refutado pela captura real de 07/10): o tipo vem SEMPRE de
/// dado persistido (NfePendente.TipoNota), nunca de adivinhar pela resposta.
/// A Focus responde NFC-e tambem em /v2/nfe, mas isso nao e documentado e
/// nao pode ser usado como prova de nada.
/// </summary>
public enum FocusDocumentType
{
    Nfce = 1,
    Nfe = 2
}

public static class FocusDocumentTypes
{
    /// <summary>
    /// Converte o texto persistido ("NFCE"/"NFE") no tipo. Estrito de
    /// proposito: valor desconhecido lanca excecao em vez de cair num
    /// endpoint por padrao, porque consultar o endpoint errado em silencio
    /// e exatamente o tipo de erro que esta etapa existe para eliminar.
    /// </summary>
    public static FocusDocumentType FromTipoNota(string? tipoNota)
    {
        return tipoNota?.Trim().ToUpperInvariant() switch
        {
            "NFCE" => FocusDocumentType.Nfce,
            "NFE" => FocusDocumentType.Nfe,
            _ => throw new ArgumentOutOfRangeException(
                nameof(tipoNota), tipoNota, "TipoNota desconhecido; esperado NFCE ou NFE.")
        };
    }
}
