using ERP.Application.Interfaces;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeStatusService : INfeStatusService
{
    private readonly IFocusNfeHttpClient _httpClient;
    
    public NfeStatusService(IFocusNfeHttpClient httpClient) => _httpClient = httpClient;

    public async Task<(bool Sucesso, string Status, string UrlDanfe, string Chave, string Numero, string UrlXml, string TipoDocumentoEncontrado)> ConsultarStatusNotaAsync(string referencia, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "", "", "", "", "", "");
        
        _httpClient.SetApiToken(token);
        string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";

        // Tenta achar NF-e A4 primeiro
        var responseResult = await _httpClient.GetAsync($"{baseServidor}/v2/nfe/{referencia}");
        string tipoEncontrado = "NFE";

        // Se deu 404, tenta achar NFC-e
        if (responseResult.IsFailed && responseResult.Errors[0].Message.Contains("404"))
        {
            responseResult = await _httpClient.GetAsync($"{baseServidor}/v2/nfce/{referencia}");
            tipoEncontrado = "NFCE";
        }

        if (responseResult.IsSuccess)
        {
            using var doc = JsonDocument.Parse(responseResult.Value);
            string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            string urlRelativa = doc.RootElement.TryGetProperty("caminho_danfe", out var u) ? u.GetString() ?? "" : "";
            string urlXmlRelativa = doc.RootElement.TryGetProperty("caminho_xml_nota_fiscal", out var x) ? x.GetString() ?? "" : "";
            string chave  = LimparChaveAcesso(doc.RootElement.TryGetProperty("chave_nfe", out var ch) ? ch.GetString() : null);
            string numero = doc.RootElement.TryGetProperty("numero", out var nu) ? nu.GetString() ?? "" : "";
            return (true, status,
                string.IsNullOrEmpty(urlRelativa) ? "" : $"{baseServidor}{urlRelativa}",
                chave, numero,
                string.IsNullOrEmpty(urlXmlRelativa) ? "" : $"{baseServidor}{urlXmlRelativa}",
                tipoEncontrado);
        }
        return (false, "Erro ao consultar", "", "", "", "", "");
    }

    /// <summary>Ver comentário no NfceEmissionService — mesmo achado, mesmo fix.</summary>
    private static string LimparChaveAcesso(string? chave)
    {
        if (string.IsNullOrWhiteSpace(chave)) return "";
        var digitos = new string(chave.Where(char.IsDigit).ToArray());
        return digitos.Length > 44 ? digitos[^44..] : digitos;
    }

    public async Task<string> ConsultarMotivoRejeicaoAsync(string referencia, string token, bool isProducao)
    {
        var result = await ConsultarStatusNotaAsync(referencia, token, isProducao);
        return result.Sucesso ? "Consulte o status completo na SEFAZ." : "Nota não encontrada.";
    }
}