// ── ERP.WPF/Services/WpfEstoqueSyncService.cs ───────────────────────────────
using ERP.Application.Interfaces;
using ERP.WPF.State;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ERP.WPF.Services;

/// <summary>
/// Implementação de IEstoqueSyncService só pro processo do WPF. O SaleService
/// que roda dentro do WPF (venda no PDV local) fala direto com o banco via
/// EF Core, sem passar pela API — mas sincronizar estoque com o Mercado
/// Livre precisa de token OAuth, dispatcher, tudo que só existe do lado da
/// API. Em vez de duplicar essa lógica aqui, esse serviço só chama a API
/// (POST /api/products/{id}/sincronizar-estoque), que faz o trabalho de
/// verdade. Mesma classe IEstoqueSyncService injetada em SaleService nos
/// dois processos — só a implementação muda por processo (DI normal).
/// </summary>
public class WpfEstoqueSyncService : IEstoqueSyncService
{
    public async Task SincronizarProdutoAsync(Guid productId)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.JwtToken);

            var resp = await http.PostAsync(
                $"{AppSession.ApiBaseUrl}/api/products/{productId}/sincronizar-estoque", null);

            if (!resp.IsSuccessStatusCode)
                Log.Warning("Falha ao sincronizar estoque do produto {ProductId} via API: {Status}",
                    productId, resp.StatusCode);
        }
        catch (Exception ex)
        {
            // Best-effort, igual a implementação da API — Mercado Livre (ou até
            // a própria API) fora do ar não pode travar uma venda no PDV.
            Log.Warning(ex, "Erro ao sincronizar estoque do produto {ProductId} via API", productId);
        }
    }
}
