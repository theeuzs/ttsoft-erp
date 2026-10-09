using ERP.Application.DTOs.FocusNfe;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Monta o corpo do POST de NFC-e a partir do PayloadJson guardado, EXATAMENTE como o envio de
/// hoje: o NfeContingencyHostedService desserializa o payload com Newtonsoft (padrao) e o
/// FocusNfeHttpClient.PostAsync serializa o objeto com System.Text.Json (padrao), tipado como
/// object. Reproduzir os dois passos aqui garante que o corpo enviado pela recuperacao e o mesmo
/// de qualquer outro caminho (ha teste comparando com o FocusNfeHttpClient real).
/// </summary>
public static class NfceCorpoDeEnvio
{
    public static string Montar(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload vazio.", nameof(payloadJson));

        var request = Newtonsoft.Json.JsonConvert.DeserializeObject<FocusNfceRequest>(payloadJson)
            ?? throw new InvalidOperationException("O payload da NFC-e desserializou como nulo.");

        return System.Text.Json.JsonSerializer.Serialize((object)request);
    }
}
