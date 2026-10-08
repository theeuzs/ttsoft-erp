namespace ERP.Application.Fiscal.Focus;

/// <summary>
/// Cliente da Focus que devolve resposta ESTRUTURADA (FocusResponse). Aditivo:
/// o IFocusNfeHttpClient atual (Result&lt;string&gt;) nao muda, para nao afetar
/// os demais consumidores (NFS-e, cancelamento, carta de correcao, NF-e recebida).
/// </summary>
public interface IFocusReferenceClient
{
    /// <summary>
    /// GET de consulta por referencia, no endpoint do tipo informado.
    /// Falha de transporte NAO lanca: volta como FocusResponse com
    /// HttpStatus 0 e TransportError. Cancelamento pelo chamador lanca
    /// OperationCanceledException. Token ou referencia vazios lancam
    /// ArgumentException (erro de configuracao do chamador, nao de rede).
    /// </summary>
    Task<FocusResponse> ConsultarAsync(
        FocusDocumentType tipo,
        string referencia,
        string token,
        bool isProducao,
        CancellationToken ct = default);

    /// <summary>
    /// POST de emissao de NFC-e em {host}/v2/nfce?ref={ref}. O corpo e repassado EXATAMENTE como
    /// veio (UTF-8, application/json): este cliente NAO desserializa, NAO reserializa e NAO valida
    /// o JSON. NAO ha retry: repetir o envio e papel da fila de recuperacao, que consulta (GET)
    /// antes de qualquer novo POST. Falha de transporte NAO lanca: volta como FocusResponse com
    /// HttpStatus 0 e TransportError. Cancelamento pelo chamador lanca OperationCanceledException
    /// e nunca vira falha transitoria. Token, referencia ou corpo vazios lancam ArgumentException
    /// antes de qualquer I/O.
    /// </summary>
    Task<FocusResponse> EnviarNfceAsync(
        string referencia,
        string jsonBody,
        string token,
        bool isProducao,
        CancellationToken ct = default);
}
