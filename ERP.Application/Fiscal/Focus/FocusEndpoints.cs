namespace ERP.Application.Fiscal.Focus;

/// <summary>Montagem pura dos enderecos da Focus. Sem I/O.</summary>
public static class FocusEndpoints
{
    public const string HostProducao = "https://api.focusnfe.com.br";
    public const string HostHomologacao = "https://homologacao.focusnfe.com.br";

    public static string Host(bool isProducao) => isProducao ? HostProducao : HostHomologacao;

    /// <summary>
    /// GET de consulta por referencia. NFCE -> /v2/nfce/{ref}; NFE -> /v2/nfe/{ref}.
    /// O endpoint e escolhido pelo tipo conhecido, nunca por tentativa e erro.
    /// </summary>
    public static string Consulta(FocusDocumentType tipo, string referencia, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(referencia))
            throw new ArgumentException("Referencia obrigatoria.", nameof(referencia));

        var segmento = tipo switch
        {
            FocusDocumentType.Nfce => "nfce",
            FocusDocumentType.Nfe => "nfe",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de documento invalido.")
        };

        return $"{Host(isProducao)}/v2/{segmento}/{Uri.EscapeDataString(referencia.Trim())}";
    }

    /// <summary>
    /// POST de emissao de NFC-e: {host}/v2/nfce?ref={ref}. A referencia e escapada como dado de
    /// URL (Uri.EscapeDataString): um caractere reservado nao consegue criar outro parametro.
    /// Para as referencias de hoje (GUID, "devolucao-{guid}") o resultado e identico ao texto
    /// montado antes por NfceEmissionService.
    /// </summary>
    public static string EmissaoNfce(string referencia, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(referencia))
            throw new ArgumentException("Referencia obrigatoria.", nameof(referencia));

        return $"{Host(isProducao)}/v2/nfce?ref={Uri.EscapeDataString(referencia.Trim())}";
    }
}
