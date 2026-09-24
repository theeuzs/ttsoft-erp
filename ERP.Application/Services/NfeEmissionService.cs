using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeEmissionService : INfeEmissionService
{
    private readonly IFocusNfeHttpClient _httpClient;
    
    public NfeEmissionService(IFocusNfeHttpClient httpClient) => _httpClient = httpClient;

    public async Task<(bool Sucesso, string Mensagem, string UrlDanfe, string UrlXml, string Chave, string Numero)> EmitirNfeA4Async(string referencia, FocusNfceRequest nfe, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "Token não configurado.", "", "", "", "");
        
        _httpClient.SetApiToken(token);
        string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";
        
        var responseResult = await _httpClient.PostAsync($"{baseServidor}/v2/nfe?ref={referencia}", nfe);

        if (responseResult.IsFailed) return (false, $"Erro: {responseResult.Errors[0].Message}", "", "", "", "");

        using var doc = JsonDocument.Parse(responseResult.Value);
        string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        if (status == "autorizado")
        {
            string urlRelativa = doc.RootElement.TryGetProperty("caminho_danfe", out var u) ? u.GetString() ?? "" : "";
            string urlXmlRelativa = doc.RootElement.TryGetProperty("caminho_xml_nota_fiscal", out var x) ? x.GetString() ?? "" : "";
            // S{N} FIX — achado testando Fase C: mesmo gap do NfceEmissionService.
            string chave  = LimparChaveAcesso(doc.RootElement.TryGetProperty("chave_nfe", out var ch) ? ch.GetString() : null);
            string numero = doc.RootElement.TryGetProperty("numero", out var nu) ? nu.GetString() ?? "" : "";
            string urlXmlCompleta = string.IsNullOrWhiteSpace(urlXmlRelativa) ? "" : $"{baseServidor}{urlXmlRelativa}";
            return (true, "NF-e Autorizada com sucesso!", $"{baseServidor}{urlRelativa}", urlXmlCompleta, chave, numero);
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
                    string urlXmlRel = consultaDoc.RootElement.TryGetProperty("caminho_xml_nota_fiscal", out var xmlProp) ? xmlProp.GetString() : "";
                    string chaveConsulta  = LimparChaveAcesso(consultaDoc.RootElement.TryGetProperty("chave_nfe", out var chProp) ? chProp.GetString() : null);
                    string numeroConsulta = consultaDoc.RootElement.TryGetProperty("numero", out var nuProp) ? nuProp.GetString() ?? "" : "";
                    string urlXmlComp = string.IsNullOrWhiteSpace(urlXmlRel) ? "" : $"{baseServidor}{urlXmlRel}";
                    return (true, "NF-e Autorizada com sucesso!", $"{baseServidor}{urlRel}", urlXmlComp, chaveConsulta, numeroConsulta);
                }
            }
            return (true, "A Nota está processando na SEFAZ. Consulte o status em instantes.", "", "", "", "");
        }

        // S{N} FIX — mesmo achado do NfceEmissionService: mensagem_sefaz
        // (motivo real da rejeição) era descartada, WPF só mostrava "Status:
        // erro_autorizacao" sem dizer o que corrigir.
        string mensagemSefaz = doc.RootElement.TryGetProperty("mensagem_sefaz", out var msgProp) ? msgProp.GetString() ?? "" : "";
        string mensagemRejeicao = string.IsNullOrWhiteSpace(mensagemSefaz)
            ? $"Nota Rejeitada. Status: {status}"
            : $"Nota Rejeitada: {mensagemSefaz}";
        return (false, mensagemRejeicao, "", "", "", "");
    }

    /// <summary>Ver comentário no NfceEmissionService — mesmo achado, mesmo fix.</summary>
    private static string LimparChaveAcesso(string? chave)
    {
        if (string.IsNullOrWhiteSpace(chave)) return "";
        var digitos = new string(chave.Where(char.IsDigit).ToArray());
        return digitos.Length > 44 ? digitos[^44..] : digitos;
    }
}