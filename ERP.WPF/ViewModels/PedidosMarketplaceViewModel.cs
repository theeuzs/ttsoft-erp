// ── ERP.WPF/ViewModels/PedidosMarketplaceViewModel.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.WPF.Commands;
using ERP.WPF.State;
using Serilog;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Tela "Marketplace → Pedidos" — cruza o status do canal (ExternalStatus)
/// com o que o ERP fez com o pedido (InternalStatus/Venda). Pensada pra uso
/// diário: é aqui que o suporte confere se um pedido processou certo, e
/// reprocessa quando necessário (ex: depois de mapear um SKU que faltava).
/// </summary>
public class PedidosMarketplaceViewModel : BaseViewModel
{
    private static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ObservableCollection<PedidoMarketplaceDto> Pedidos { get; } = new();

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ICommand AtualizarCommand   { get; }
    public ICommand ReprocessarCommand { get; }

    public PedidosMarketplaceViewModel()
    {
        AtualizarCommand   = new AsyncRelayCommand(async _ => await CarregarAsync());
        ReprocessarCommand = new AsyncRelayCommand(async param => await ReprocessarAsync(param));

        _ = CarregarAsync();
    }

    private static HttpClient CriarHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.JwtToken);
        return http;
    }

    private async Task CarregarAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = CriarHttpClient();
            var resp = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/marketplace/pedidos");
            resp.EnsureSuccessStatusCode();

            var pedidos = await resp.Content.ReadFromJsonAsync<List<PedidoMarketplaceDto>>(JsonOpcoes);
            Pedidos.Clear();
            if (pedidos is not null)
                foreach (var p in pedidos) Pedidos.Add(p);

            if (Pedidos.Count == 0)
                StatusMessage = "Nenhum pedido de marketplace encontrado.";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = "Não consegui carregar os pedidos.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao carregar pedidos de marketplace (tela Pedidos)");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReprocessarAsync(object? parametro)
    {
        if (IsBusy) return;
        if (parametro is not Guid pedidoId) return;

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = CriarHttpClient();
            var resp = await http.PostAsync(
                $"{AppSession.ApiBaseUrl}/api/marketplace/pedidos/{pedidoId}/reprocessar", null);

            if (!resp.IsSuccessStatusCode)
            {
                StatusMessage = $"Não consegui reprocessar: {resp.StatusCode}";
                OnPropertyChanged(nameof(HasStatusMessage));
                return;
            }

            StatusMessage = "Pedido reprocessado.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao reprocessar o pedido.";
            Log.Error(ex, "Erro ao reprocessar pedido {PedidoId} (tela Pedidos)", pedidoId);
        }
        finally
        {
            OnPropertyChanged(nameof(HasStatusMessage));
            IsBusy = false;
            await CarregarAsync(); // recarrega pra refletir o novo status
        }
    }
}