namespace ERP.Application.Fiscal.Focus;

/// <summary>
/// Leitura do tipo de documento a partir da propria chave de acesso (44
/// digitos), cujas posicoes 21-22 (base 0: 20-21) sao o MODELO fiscal:
/// 55 = NF-e, 65 = NFC-e. E o dado do proprio documento autorizado, definido
/// pela SEFAZ; nao depende de qual endpoint da Focus respondeu (a Focus
/// responde NFC-e tambem em /v2/nfe) nem de suposicoes sobre quem produz
/// cada estado no ERP.
/// </summary>
public static class FocusChaveAcesso
{
    /// <summary>
    /// Espera a chave JA normalizada (exatamente 44 digitos). Qualquer outra
    /// coisa, ou um modelo diferente de 55/65, devolve nulo: nunca adivinha.
    /// </summary>
    public static FocusDocumentType? TipoDocumento(string? chave44)
    {
        if (chave44 is null || chave44.Length != 44)
            return null;

        foreach (var c in chave44)
        {
            if (c < '0' || c > '9')
                return null;
        }

        return chave44.Substring(20, 2) switch
        {
            "65" => FocusDocumentType.Nfce,
            "55" => FocusDocumentType.Nfe,
            _ => null
        };
    }
}
