// ── ERP.WPF/ViewModels/SkuMappingViewModel.cs ───────────────────────────────
using ERP.Application.DTOs;
using ERP.Domain.Enums;
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
/// Tela "Configurações → Integrações → Gerenciar SKU Mapping" — liga cada
/// anúncio do marketplace a um Product interno, sem SQL. Escolhe o canal
/// primeiro (um lojista pode ter mais de uma conta conectada), depois lista
/// os anúncios desse canal com o status de mapeamento de cada um.
/// </summary>
public class SkuMappingViewModel : BaseViewModel
{
    // Mesma configuração de JSON do IntegracoesViewModel — a API serializa
    // enums como texto e usa camelCase; sem isso, os campos vêm zerados
    // silenciosamente (já vivemos esse bug uma vez).
    internal static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ObservableCollection<SalesChannelCardViewModel> Canais { get; } = new();
    public ObservableCollection<AnuncioMapeamentoRowViewModel> Anuncios { get; } = new();

    private SalesChannelCardViewModel? _canalSelecionado;
    public SalesChannelCardViewModel? CanalSelecionado
    {
        get => _canalSelecionado;
        set
        {
            if (SetProperty(ref _canalSelecionado, value))
                _ = CarregarAnunciosAsync();
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ICommand CarregarCanaisCommand  { get; }
    public ICommand AtualizarAnunciosCommand { get; }

    public SkuMappingViewModel()
    {
        CarregarCanaisCommand    = new AsyncRelayCommand(async _ => await CarregarCanaisAsync());
        AtualizarAnunciosCommand = new AsyncRelayCommand(async _ => await CarregarAnunciosAsync());

        _ = CarregarCanaisAsync();
    }

    internal static HttpClient CriarHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.JwtToken);
        return http;
    }

    private async Task CarregarCanaisAsync()
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

            var canais = await resp.Content.ReadFromJsonAsync<List<SalesChannelStatusDto>>(JsonOpcoes);
            Canais.Clear();
            if (canais is not null)
                foreach (var c in canais.Where(c => c.Conectado)) // só canais conectados fazem sentido pra mapear
                    Canais.Add(new SalesChannelCardViewModel(c));

            CanalSelecionado = Canais.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = "Não consegui carregar os canais conectados.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao carregar canais (tela SkuMapping)");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CarregarAnunciosAsync()
    {
        if (IsBusy || CanalSelecionado is null) return;
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = CriarHttpClient();
            var resp = await http.GetAsync(
                $"{AppSession.ApiBaseUrl}/api/saleschannels/{CanalSelecionado.Id}/anuncios");
            resp.EnsureSuccessStatusCode();

            var anuncios = await resp.Content.ReadFromJsonAsync<List<AnuncioComMapeamentoDto>>(JsonOpcoes);
            Anuncios.Clear();
            if (anuncios is not null)
                foreach (var a in anuncios)
                    Anuncios.Add(new AnuncioMapeamentoRowViewModel(a, CanalSelecionado.Id));

            if (Anuncios.Count == 0)
                StatusMessage = "Nenhum anúncio ativo encontrado nesse canal.";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = "Não consegui carregar os anúncios desse canal.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao carregar anúncios (tela SkuMapping)");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>
/// Uma linha da tela — um anúncio do marketplace, com busca e mapeamento de
/// produto embutidos. Fica "vivo" (com HttpClient próprio) porque cada linha
/// busca e mapeia de forma independente das outras.
/// </summary>
public class AnuncioMapeamentoRowViewModel : BaseViewModel
{
    private readonly Guid _salesChannelId;
    private readonly AnuncioComMapeamentoDto _dto;

    public string ItemId     => _dto.ItemId;
    public string SkuExterno => _dto.SkuExterno;
    public string Titulo     => _dto.Titulo;

    private bool _mapeado;
    public bool Mapeado { get => _mapeado; private set => SetProperty(ref _mapeado, value); }

    /// <summary>Inverso de Mapeado, só pra Visibility no XAML — BoolToVisibility
    /// é o BooleanToVisibilityConverter padrão do WPF, que não tem suporte a
    /// inverter via ConverterParameter, então preciso da própria propriedade.</summary>
    public bool NaoMapeado => !Mapeado;

    private string? _productNomeAtual;
    public string? ProductNomeAtual { get => _productNomeAtual; private set => SetProperty(ref _productNomeAtual, value); }

    private string _textoBusca = string.Empty;
    public string TextoBusca { get => _textoBusca; set => SetProperty(ref _textoBusca, value); }

    public ObservableCollection<ProductDto> ResultadosBusca { get; } = new();

    private ProductDto? _produtoSelecionado;
    public ProductDto? ProdutoSelecionado { get => _produtoSelecionado; set => SetProperty(ref _produtoSelecionado, value); }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ICommand BuscarProdutoCommand { get; }
    public ICommand MapearCommand        { get; }

    public AnuncioMapeamentoRowViewModel(AnuncioComMapeamentoDto dto, Guid salesChannelId)
    {
        _dto             = dto;
        _salesChannelId  = salesChannelId;
        _mapeado         = dto.Mapeado;
        _productNomeAtual = dto.ProductNome;

        BuscarProdutoCommand = new AsyncRelayCommand(async _ => await BuscarProdutoAsync());
        MapearCommand         = new AsyncRelayCommand(async _ => await MapearAsync());
    }

    private async Task BuscarProdutoAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(TextoBusca)) return;
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = SkuMappingViewModel.CriarHttpClient();
            var url = $"{AppSession.ApiBaseUrl}/api/products?search={Uri.EscapeDataString(TextoBusca)}&pageSize=20";
            var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var pagina = await resp.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(SkuMappingViewModel.JsonOpcoes);
            ResultadosBusca.Clear();
            if (pagina?.Items is not null)
                foreach (var p in pagina.Items) ResultadosBusca.Add(p);

            if (ResultadosBusca.Count == 0)
                StatusMessage = "Nenhum produto encontrado com esse nome.";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao buscar produtos.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao buscar produtos (SkuMapping, item {ItemId})", ItemId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MapearAsync()
    {
        if (IsBusy || ProdutoSelecionado is null) return;
        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));

            using var http = SkuMappingViewModel.CriarHttpClient();
            // Manda os dois — SkuExterno quando o anúncio tem, ItemId sempre.
            // O backend decide qual guardar (mesma lógica do ResolverSkuAsync).
            var corpo = new CriarSkuMappingDto(
                string.IsNullOrEmpty(SkuExterno) ? null : SkuExterno,
                ItemId,
                ProdutoSelecionado.Id);

            var resp = await http.PostAsJsonAsync(
                $"{AppSession.ApiBaseUrl}/api/saleschannels/{_salesChannelId}/mapeamentos", corpo, SkuMappingViewModel.JsonOpcoes);

            if (!resp.IsSuccessStatusCode)
            {
                var detalhe = await resp.Content.ReadAsStringAsync();
                StatusMessage = $"Não consegui mapear: {detalhe}";
                OnPropertyChanged(nameof(HasStatusMessage));
                return;
            }

            Mapeado          = true;
            OnPropertyChanged(nameof(NaoMapeado));
            ProductNomeAtual = ProdutoSelecionado.Name;
            ResultadosBusca.Clear();
            TextoBusca = string.Empty;
            StatusMessage = "Mapeado com sucesso.";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao mapear produto.";
            OnPropertyChanged(nameof(HasStatusMessage));
            Log.Error(ex, "Erro ao mapear (SkuMapping, item {ItemId})", ItemId);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
