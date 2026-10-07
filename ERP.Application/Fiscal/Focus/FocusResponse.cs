namespace ERP.Application.Fiscal.Focus;

/// <summary>
/// Resposta estruturada da Focus (Etapa 4). Imutavel. Produzida por
/// FocusResponseParser; nao conhece venda, banco nem regra de negocio.
///
/// Dois mundos distintos, mutuamente exclusivos:
///  - houve resposta HTTP: HttpStatus > 0, TransportError nulo;
///  - nao houve (timeout, DNS, conexao recusada): HttpStatus == 0 e
///    TransportError preenchido.
/// </summary>
public sealed record FocusResponse
{
    /// <summary>Status HTTP; 0 quando nao houve resposta HTTP.</summary>
    public int HttpStatus { get; init; }

    /// <summary>Descricao da falha de transporte; nulo quando houve resposta HTTP.</summary>
    public string? TransportError { get; init; }

    /// <summary>
    /// Corpo exatamente como recebido, para diagnostico. Ao persistir,
    /// TRUNCAR: nao gravar o corpo bruto inteiro no banco.
    /// </summary>
    public string RawBody { get; init; } = string.Empty;

    // Corpo de erro (4xx): {"codigo": "...", "mensagem": "..."}
    public string? Codigo { get; init; }
    public string? Mensagem { get; init; }

    // Corpo de documento (200): status do documento e retorno da SEFAZ.
    public string? Status { get; init; }
    public string? StatusSefaz { get; init; }
    public string? MensagemSefaz { get; init; }

    /// <summary>Chave de acesso normalizada (44 digitos) ou nulo.</summary>
    public string? ChaveNfe { get; init; }

    /// <summary>Valor de chave_nfe como veio (ex.: com prefixo "NFe"), para auditoria.</summary>
    public string? ChaveNfeBruta { get; init; }

    public string? Numero { get; init; }
    public string? Serie { get; init; }
    public string? Protocolo { get; init; }

    /// <summary>Caminhos RELATIVOS; o consumidor monta a URL com o host, como hoje.</summary>
    public string? CaminhoXmlNotaFiscal { get; init; }
    public string? CaminhoDanfe { get; init; }

    public bool HouveRespostaHttp => TransportError is null && HttpStatus > 0;
}
