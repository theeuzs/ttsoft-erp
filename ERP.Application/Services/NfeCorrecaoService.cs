using ERP.Application.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeCorrecaoService : INfeCorrecaoService
{
    private readonly IFocusNfeHttpClient _httpClient;

    public NfeCorrecaoService(IFocusNfeHttpClient httpClient) => _httpClient = httpClient;

    public async Task<(bool Sucesso, string Mensagem, string? UrlPdf)> EmitirCartaCorrecaoAsync(
        string referencia, string textoCorrecao, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Token da Focus NFe não configurado.", null);

        if (string.IsNullOrWhiteSpace(textoCorrecao) || textoCorrecao.Length < 15)
            return (false, "O texto da correção precisa ter no mínimo 15 caracteres.", null);

        _httpClient.SetApiToken(token);
        string baseServidor = isProducao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";
        string endpoint = $"{baseServidor}/v2/nfe/{referencia}/carta_correcao";

        var responseResult = await _httpClient.PostAsync(endpoint, new { correcao = textoCorrecao });

        if (responseResult.IsFailed)
            return (false, $"Erro de Comunicação: {responseResult.Errors[0].Message}", null);

        using var doc = JsonDocument.Parse(responseResult.Value);
        var root = doc.RootElement;

        // A Focus retorna "status" nas outras operações; a carta de correção
        // é síncrona e devolve sucesso/mensagem diretamente.
        bool sucesso = root.TryGetProperty("sucesso", out var s) ? s.GetBoolean() : true;
        string mensagem = root.TryGetProperty("mensagem", out var m) ? m.GetString() ?? "" : "Carta de correção registrada.";

        if (!sucesso)
            return (false, mensagem, null);

        string? urlRelativa = root.TryGetProperty("caminho_pdf_carta_correcao", out var u) ? u.GetString() : null;
        string? urlCompleta = string.IsNullOrWhiteSpace(urlRelativa) ? null : $"{baseServidor}{urlRelativa}";

        return (true, string.IsNullOrWhiteSpace(mensagem) ? "Carta de correção registrada com sucesso!" : mensagem, urlCompleta);
    }
}
