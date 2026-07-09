using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ERP.WPF.Helpers;

/// <summary>
/// Polling opcional que verifica se o Pix foi pago via API do provedor.
/// Suporta: OpenPix / Gerencianet (EFÍ) / PagBank.
/// Se nenhuma chave de API estiver configurada, funciona apenas com confirmação manual.
/// </summary>
public class PixPollingService : IDisposable
{
    private readonly HttpClient _http;
    private readonly TimeSpan _intervalo;
    private readonly Action<Exception, string, string> _onErro;
    private CancellationTokenSource? _cts;

    /// <summary>Evento disparado quando o pagamento é confirmado pela API.</summary>
    public event Action? PagamentoConfirmado;

    // S15 FIX (testabilidade): handler, intervalo e callback de erro agora são
    // injetáveis, todos com default que preservam o comportamento de produção
    // exatamente como era antes — nenhum caller existente (WPF usa
    // "new PixPollingService()" sem argumentos) precisa mudar.
    // Isso permite testar o comportamento do catch (loop continua, log dispara)
    // sem esperar os 5s reais e sem bater numa API de verdade.
    public PixPollingService(
        HttpMessageHandler? handler = null,
        TimeSpan? intervalo = null,
        Action<Exception, string, string>? onErro = null)
    {
        _http      = handler is null ? new HttpClient() : new HttpClient(handler);
        _intervalo = intervalo ?? TimeSpan.FromSeconds(5);
        _onErro    = onErro ?? DefaultOnErro;
    }

    private static void DefaultOnErro(Exception ex, string txid, string provedor)
    {
        // S15 FIX: antes esse catch era mudo (catch { }) — qualquer erro,
        // esperado (rede instável) ou não (token expirado, mudança de
        // contrato da API do provedor, bug de parsing), desaparecia sem
        // deixar rastro. Loga como Warning (não Error) porque falha
        // pontual de rede É esperada aqui e não deve gerar alerta de
        // produção — mas fica visível em log pra quem for investigar
        // "por que essa venda ficou esperando confirmação de Pix".
        Log.Warning(ex,
            "PixPollingService: falha ao verificar status do Pix (txid={Txid}, provedor={Provedor}) " +
            "— tenta de novo no próximo ciclo", txid, provedor);
    }

    /// <summary>
    /// Inicia o polling para verificar o status do Pix.
    /// Verifica a cada 5 segundos (ou o intervalo configurado) por até 3 minutos.
    /// </summary>
    public void IniciarPolling(string txid, string? apiToken, string? provedor = "openpix")
    {
        if (string.IsNullOrWhiteSpace(apiToken)) return; // Sem token = só manual

        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        _ = Task.Run(() => PollingLoop(txid, apiToken, provedor ?? "openpix", _cts.Token));
    }

    private async Task PollingLoop(string txid, string apiToken, string provedor, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_intervalo, ct);

                bool pago = provedor.ToLower() switch
                {
                    "openpix"     => await VerificarOpenPix(txid, apiToken, ct),
                    "gerencianet" => await VerificarGerencianet(txid, apiToken, ct),
                    _             => false
                };

                if (pago)
                {
                    PagamentoConfirmado?.Invoke();
                    return;
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _onErro(ex, txid, provedor);
            }
        }
    }

    private async Task<bool> VerificarOpenPix(string txid, string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.openpix.com.br/api/v1/charge/{txid}");
        req.Headers.Add("Authorization", token);

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return false;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        // OpenPix: charge.status == "COMPLETED"
        if (doc.RootElement.TryGetProperty("charge", out var charge) &&
            charge.TryGetProperty("status", out var status))
            return status.GetString() == "COMPLETED";

        return false;
    }

    private async Task<bool> VerificarGerencianet(string txid, string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"https://pix.api.efipay.com.br/v2/cob/{txid}");
        req.Headers.Add("Authorization", $"Bearer {token}");

        var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return false;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        // EFÍ: status == "CONCLUIDA"
        if (doc.RootElement.TryGetProperty("status", out var status))
            return status.GetString() == "CONCLUIDA";

        return false;
    }

    public void Parar() => _cts?.Cancel();

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _http.Dispose();
    }
}