using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using System.Linq;
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

    public async Task<(bool Sucesso, string Mensagem, string UrlDanfe, string UrlXml, string Chave, string Numero)> EmitirNfceAsync(string referencia, FocusNfceRequest nfce, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "O Token da Focus NFe não foi configurado.", "", "", "", "");

        // 🟢 Passando o Token do jeito certo (Oculto e Seguro)
        _httpClient.SetApiToken(token);
        
        string endpoint = isProducao ? $"https://api.focusnfe.com.br/v2/nfce?ref={referencia}" : $"https://homologacao.focusnfe.com.br/v2/nfce?ref={referencia}";

        // O Polly tenta fazer a requisição de forma resiliente
        var responseResult = await _httpClient.PostAsync(endpoint, nfce);

        if (responseResult.IsFailed)
            return (false, $"Erro de Comunicação: {responseResult.Errors[0].Message}", "", "", "", "");

        using var doc = JsonDocument.Parse(responseResult.Value);
        var root = doc.RootElement;
        
        string status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "";

        if (status == "autorizado")
        {
            string urlRelativa = root.TryGetProperty("caminho_danfe", out var urlProp) ? urlProp.GetString() : "";
            string urlXmlRelativa = root.TryGetProperty("caminho_xml_nota_fiscal", out var xmlProp) ? xmlProp.GetString() : "";
            // S{N} FIX — achado testando Fase C: chave_nfe/numero vinham na
            // resposta e eram descartados, deixando Sale.NfceChave/NfceNumero
            // sempre NULL mesmo com a nota autorizada.
            string chave  = LimparChaveAcesso(root.TryGetProperty("chave_nfe", out var chaveProp) ? chaveProp.GetString() : null);
            string numero = root.TryGetProperty("numero", out var numProp) ? numProp.GetString() ?? "" : "";
            string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";
            string urlXmlCompleta = string.IsNullOrWhiteSpace(urlXmlRelativa) ? "" : $"{baseServidor}{urlXmlRelativa}";
            return (true, "NFC-e Autorizada com sucesso!", $"{baseServidor}{urlRelativa}", urlXmlCompleta, chave, numero);
        }

        // S{N} FIX — achado testando em produção: mesmo gap do chave_nfe/
        // numero, mas na mensagem de rejeição. A Focus manda o motivo real
        // em mensagem_sefaz ("CFOP nao permitido...", "NCM inexistente...",
        // com o item afetado) e isso era descartado — o WPF só mostrava
        // "Status: erro_autorizacao", sem dizer o que corrigir na venda.
        string mensagemSefaz = root.TryGetProperty("mensagem_sefaz", out var msgProp) ? msgProp.GetString() ?? "" : "";
        string mensagemRejeicao = string.IsNullOrWhiteSpace(mensagemSefaz)
            ? $"Nota Rejeitada. Status: {status}"
            : $"Nota Rejeitada: {mensagemSefaz}";
        return (false, mensagemRejeicao, "", "", "", "");
    }

    /// <summary>
    /// Achado testando em produção: uma chave real veio como
    /// "NFe41260912820608000141650010000033031335484324" — o valor puro
    /// (44 dígitos) estava lá, só com um prefixo não numérico grudado.
    /// Extrai só os dígitos e mantém os últimos 44 (tamanho de uma chave de
    /// acesso de verdade), pra nunca salvar prefixo nenhum, seja "NFe" ou
    /// qualquer outra coisa que a Focus decida colar na frente no futuro.
    /// </summary>
    private static string LimparChaveAcesso(string? chave)
    {
        if (string.IsNullOrWhiteSpace(chave)) return "";
        var digitos = new string(chave.Where(char.IsDigit).ToArray());
        return digitos.Length > 44 ? digitos[^44..] : digitos;
    }
}