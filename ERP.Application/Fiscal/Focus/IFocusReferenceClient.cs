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
}
