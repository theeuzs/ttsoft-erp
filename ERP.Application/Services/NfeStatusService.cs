using ERP.Application.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeStatusService : INfeStatusService
{
    private readonly IFocusNfeHttpClient _httpClient;
    
    public NfeStatusService(IFocusNfeHttpClient httpClient) => _httpClient = httpClient;

    public async Task<(bool Sucesso, string Status, string UrlDanfe)> ConsultarStatusNotaAsync(string referencia, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "", "");
        
        _httpClient.SetApiToken(token);
        string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";

        // Tenta achar NF-e A4 primeiro
        var responseResult = await _httpClient.GetAsync($"{baseServidor}/v2/nfe/{referencia}");
        
        // Se deu 404, tenta achar NFC-e
        if (responseResult.IsFailed && responseResult.Errors[0].Message.Contains("404"))
            responseResult = await _httpClient.GetAsync($"{baseServidor}/v2/nfce/{referencia}");

        if (responseResult.IsSuccess)
        {
            using var doc = JsonDocument.Parse(responseResult.Value);
            string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            string urlRelativa = doc.RootElement.TryGetProperty("caminho_danfe", out var u) ? u.GetString() ?? "" : "";
            return (true, status, string.IsNullOrEmpty(urlRelativa) ? "" : $"{baseServidor}{urlRelativa}");
        }
        return (false, "Erro ao consultar", "");
    }

    public async Task<string> ConsultarMotivoRejeicaoAsync(string referencia, string token, bool isProducao)
    {
        var result = await ConsultarStatusNotaAsync(referencia, token, isProducao);
        return result.Sucesso ? "Consulte o status completo na SEFAZ." : "Nota não encontrada.";
    }
}