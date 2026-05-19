using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeEmissionService : INfeEmissionService
{
    private readonly IFocusNfeHttpClient _httpClient;
    
    public NfeEmissionService(IFocusNfeHttpClient httpClient) => _httpClient = httpClient;

    public async Task<(bool Sucesso, string Mensagem, string UrlDanfe)> EmitirNfeA4Async(string referencia, FocusNfceRequest nfe, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "Token não configurado.", "");
        
        _httpClient.SetApiToken(token);
        string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";
        
        var responseResult = await _httpClient.PostAsync($"{baseServidor}/v2/nfe?ref={referencia}", nfe);

        if (responseResult.IsFailed) return (false, $"Erro: {responseResult.Errors[0].Message}", "");

        using var doc = JsonDocument.Parse(responseResult.Value);
        string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        if (status == "autorizado")
        {
            string urlRelativa = doc.RootElement.TryGetProperty("caminho_danfe", out var u) ? u.GetString() ?? "" : "";
            return (true, "NF-e Autorizada com sucesso!", $"{baseServidor}{urlRelativa}");
        }
        else if (status == "processando_autorizacao")
        {
            // O Truque do ERP Sênior: Espera 3s e tenta pegar o PDF de novo
            await Task.Delay(3000); 
            var consultaResult = await _httpClient.GetAsync($"{baseServidor}/v2/nfe/{referencia}");
            if (consultaResult.IsSuccess)
            {
                using var consultaDoc = JsonDocument.Parse(consultaResult.Value);
                if (consultaDoc.RootElement.TryGetProperty("status", out var cs) && cs.GetString() == "autorizado")
                {
                    string urlRel = consultaDoc.RootElement.TryGetProperty("caminho_danfe", out var urlProp) ? urlProp.GetString() : "";
                    return (true, "NF-e Autorizada com sucesso!", $"{baseServidor}{urlRel}");
                }
            }
            return (true, "A Nota está processando na SEFAZ. Consulte o status em instantes.", "");
        }

        return (false, $"Nota Rejeitada. Status: {status}", "");
    }
}