using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfceEmissionService : INfceEmissionService
{
    private readonly IFocusNfeHttpClient _httpClient;

    public NfceEmissionService(IFocusNfeHttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(bool Sucesso, string Mensagem, string UrlDanfe)> EmitirNfceAsync(string referencia, FocusNfceRequest nfce, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "O Token da Focus NFe não foi configurado.", "");

        // 🟢 Passando o Token do jeito certo (Oculto e Seguro)
        _httpClient.SetApiToken(token);
        
        string endpoint = isProducao ? $"https://api.focusnfe.com.br/v2/nfce?ref={referencia}" : $"https://homologacao.focusnfe.com.br/v2/nfce?ref={referencia}";

        // O Polly tenta fazer a requisição de forma resiliente
        var responseResult = await _httpClient.PostAsync(endpoint, nfce);

        if (responseResult.IsFailed)
            return (false, $"Erro de Comunicação: {responseResult.Errors[0].Message}", "");

        using var doc = JsonDocument.Parse(responseResult.Value);
        var root = doc.RootElement;
        
        string status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "";

        if (status == "autorizado")
        {
            string urlRelativa = root.TryGetProperty("caminho_danfe", out var urlProp) ? urlProp.GetString() : "";
            string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";
            return (true, "NFC-e Autorizada com sucesso!", $"{baseServidor}{urlRelativa}");
        }

        return (false, $"Nota Rejeitada. Status: {status}", "");
    }
}