using ERP.Application.Interfaces;
using System.Text.Json;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class NfeCancellationService : INfeCancellationService
{
    private readonly IFocusNfeHttpClient _httpClient;
    
    public NfeCancellationService(IFocusNfeHttpClient httpClient) => _httpClient = httpClient;

    public async Task<(bool Sucesso, string Mensagem)> CancelarNotaAsync(string referencia, string justificativa, string token, bool isProducao)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(justificativa) || justificativa.Length < 15)
            return (false, "Token inválido ou justificativa menor que 15 caracteres.");

        _httpClient.SetApiToken(token);
        string endpoint = isProducao ? $"https://api.focusnfe.com.br/v2/nfce/{referencia}" : $"https://homologacao.focusnfe.com.br/v2/nfce/{referencia}";

        // 🟢 CORREÇÃO: Chama o método Delete com Body passando a justificativa
        var responseResult = await _httpClient.DeleteWithBodyAsync(endpoint, new { justificativa = justificativa });

        if (responseResult.IsFailed) return (false, $"Erro: {responseResult.Errors[0].Message}");

        using var doc = JsonDocument.Parse(responseResult.Value);
        string status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        if (status == "cancelado") return (true, "Nota cancelada com sucesso!");
        
        return (true, $"Pedido enviado. Status atual: {status}");
    }
}