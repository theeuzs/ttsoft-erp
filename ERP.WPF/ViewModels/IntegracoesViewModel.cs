// ── ERP.WPF/ViewModels/IntegracoesViewModel.cs ──────────────────────────────
using ERP.Application.DTOs;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using ERP.WPF.State;
using Serilog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Tela "Configurações → Integrações" — permite ao próprio lojista conectar
/// um marketplace (hoje só Mercado Livre) sem precisar de SQL, GUID copiado
/// manualmente ou acesso ao Azure. Fala com a API via HTTP (não EF Core
/// direto) porque o fluxo OAuth exige um callback público — só a API
/// publicada consegue receber isso, o WPF sozinho nunca conseguiria.
/// </summary>
public class IntegracoesViewModel : BaseViewModel
{
    public ObservableCollection<SalesChannelCardViewModel> Canais { get; } = new();

    /// <summary>Usado só pra Visibility do TextBlock de status — StatusMessage
    /// (da BaseViewModel) começa em string.Empty, não null, então o conversor
    /// NullToCollapsed sozinho não escondia a mensagem vazia.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ICommand AtualizarCommand             { get; }
    public ICommand ConectarMercadoLivreCommand  { get; }
    public ICommand SincronizarAgoraCommand      { get; }

    public IntegracoesViewModel()
    {
        AtualizarCommand            = new AsyncRelayCommand(async _ => await CarregarAsync());
        ConectarMercadoLivreCommand  = new AsyncRelayCommand(async _ => await ConectarMercadoLivreAsync());
        SincronizarAgoraCommand      = new AsyncRelayCommand(async param => await SincronizarAgoraAsync(param));

        _ = CarregarAsync(); // dispara ao abrir a tela — fire-and-forget, igual ao padrão do ChatService
    }

    // A API serializa enums como texto (JsonStringEnumConverter em Program.cs) —
    // sem isso aqui, o HttpClient assume número por padrão e quebra na leitura.
    private static readonly JsonSerializerOptions _jsonOpcoes = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
            var resp = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/saleschannels");
            resp.EnsureSuccessStatusCode();

            var canais = await resp.Content.ReadFromJsonAsync<List<SalesChannelStatusDto>>(_jsonOpcoes);
            Canais.Clear();
            if (canais is not null)
                foreach (var c in canais) Canais.Add(new SalesChannelCardViewModel(c));
        }
        catch (Exception ex)
        {
            // DIAGNÓSTICO TEMPORÁRIO — volta pra mensagem genérica depois de descobrir a causa.
            var detalhe = ex.InnerException?.Message ?? ex.Message;
            StatusMessage = $"[DIAG] {ex.GetType().Name}: {detalhe}";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao carregar canais de venda (tela Integrações)");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConectarMercadoLivreAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = CriarHttpClient();
            var corpo = new CriarSalesChannelDto(SalesChannelType.MercadoLivre, "Mercado Livre", true);
            var resp  = await http.PostAsJsonAsync($"{AppSession.ApiBaseUrl}/api/saleschannels", corpo, _jsonOpcoes);
            resp.EnsureSuccessStatusCode();

            var criado = await resp.Content.ReadFromJsonAsync<SalesChannelCriadoDto>(_jsonOpcoes);
            if (criado?.AuthorizationUrl is null)
            {
                StatusMessage = "Canal criado, mas esse tipo ainda não tem conexão automática.";
            OnPropertyChanged(nameof(HasStatusMessage));
            }
            else
            {
                // Abre no navegador padrão — o lojista loga com a conta real
                // dele no Mercado Livre e autoriza; o callback na API cuida do resto.
                Process.Start(new ProcessStartInfo(criado.AuthorizationUrl) { UseShellExecute = true });
                StatusMessage = "Autorize no navegador que abriu. Depois, clique em \"Atualizar\".";
            OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Não consegui conectar ao Mercado Livre — tente novamente.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao criar/conectar canal Mercado Livre (tela Integrações)");
        }
        finally
        {
            IsBusy = false;
            await CarregarAsync();
        }
    }

    private async Task SincronizarAgoraAsync(object? parametro)
    {
        if (IsBusy) return;
        if (parametro is not Guid salesChannelId) return;

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = CriarHttpClient();
            var desde = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            var resp  = await http.PostAsync(
                $"{AppSession.ApiBaseUrl}/api/marketplace/ml/sincronizar/{salesChannelId}?desde={desde}", null);
            resp.EnsureSuccessStatusCode();

            StatusMessage = "Sincronização concluída.";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = "Não consegui sincronizar agora — tente de novo em alguns minutos.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao sincronizar canal {SalesChannelId} (tela Integrações)", salesChannelId);
        }
        finally
        {
            IsBusy = false;
            await CarregarAsync();
        }
    }
}

/// <summary>
/// Wrapper de exibição — o DTO da API (SalesChannelStatusDto) fica cru e
/// reutilizável em qualquer front; as strings prontas pra tela ficam aqui,
/// só no WPF.
/// </summary>
public class SalesChannelCardViewModel
{
    private readonly SalesChannelStatusDto _dto;
    public SalesChannelCardViewModel(SalesChannelStatusDto dto) => _dto = dto;

    public Guid                Id                => _dto.Id;
    public SalesChannelType    Tipo              => _dto.Tipo;
    public string               Nome              => _dto.Nome;
    public bool                 Conectado         => _dto.Conectado;
    public string               ExternalAccountId => _dto.ExternalAccountId ?? "—";

    public string StatusConectado
        => _dto.Conectado ? "✔ Conectado" : "○ Não conectado";

    public string StatusToken
        => !_dto.Conectado ? "—" : _dto.TokenValido ? "✔ Válido" : "⚠ Expirado (renova sozinho no próximo uso)";

    public string StatusUltimaSincronizacao
        => _dto.UltimaSincronizacao is null
            ? "Nunca sincronizado"
            : $"{_dto.UltimaSincronizacao:dd/MM HH:mm} ({_dto.UltimoTotalProcessados ?? 0} pedido(s) novo(s))";
}