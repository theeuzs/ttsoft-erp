// ERP.WPF/ViewModels/MarketplaceDashboardViewModel.cs
using ERP.Application.DTOs;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using ERP.WPF.State;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>Uma linha do gráfico de status — contagem + rótulo em português,
/// já pronta pra tela, sem a UI precisar interpretar o enum.</summary>
public record StatusResumo(string Rotulo, int Quantidade, string Cor);

/// <summary>Uma linha de faturamento por canal.</summary>
public record CanalResumo(string Nome, decimal Faturamento, int Pedidos);

/// <summary>
/// Dashboard de Marketplace — reagrega o mesmo endpoint que a tela "Pedidos"
/// já usa (GET /api/marketplace/pedidos), sem rota nova na API. Prioridades
/// confirmadas com o dono do negócio (08/2026): "precisa de atenção" no
/// topo, clicável, e Estoque Reservado com destaque visual próprio — pra
/// loja física de material de construção, estoque preso pro marketplace é
/// espaço ocupado que não pode ser vendido no balcão.
/// </summary>
public class MarketplaceDashboardViewModel : BaseViewModel
{
    private static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private List<PedidoMarketplaceDto> _todosPedidos = new();

    public ObservableCollection<PedidoMarketplaceDto> PrecisaAtencao { get; } = new();
    public ObservableCollection<StatusResumo> StatusBreakdown { get; } = new();
    public ObservableCollection<CanalResumo> FaturamentoPorCanal { get; } = new();

    private int _totalPedidos;
    public int TotalPedidos { get => _totalPedidos; set => SetProperty(ref _totalPedidos, value); }

    private decimal _faturamentoTotal;
    public decimal FaturamentoTotal { get => _faturamentoTotal; set => SetProperty(ref _faturamentoTotal, value); }

    private int _pedidosAtencao;
    public int PedidosAtencao { get => _pedidosAtencao; set => SetProperty(ref _pedidosAtencao, value); }

    private int _estoqueReservadoQtd;
    /// <summary>Destaque próprio (não só mais uma linha do StatusBreakdown) —
    /// prioridade confirmada: volume preso no marketplace é espaço físico
    /// que a loja não pode vender no balcão enquanto isso.</summary>
    public int EstoqueReservadoQtd { get => _estoqueReservadoQtd; set => SetProperty(ref _estoqueReservadoQtd, value); }

    private string _lojasConectadasTexto = string.Empty;
    public string LojasConectadasTexto { get => _lojasConectadasTexto; set => SetProperty(ref _lojasConectadasTexto, value); }

    public string[] Periodos { get; } = { "Hoje", "7 dias", "30 dias", "Tudo" };
    private string _periodoSelecionado = "7 dias";
    public string PeriodoSelecionado
    {
        get => _periodoSelecionado;
        set { SetProperty(ref _periodoSelecionado, value); RecalcularResumos(); }
    }

    public ICommand AtualizarCommand { get; }

    public MarketplaceDashboardViewModel()
    {
        AtualizarCommand = new AsyncRelayCommand(async _ => await CarregarAsync());
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
            using var http = CriarHttpClient();

            var respPedidos = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/marketplace/pedidos");
            respPedidos.EnsureSuccessStatusCode();
            _todosPedidos = await respPedidos.Content.ReadFromJsonAsync<List<PedidoMarketplaceDto>>(JsonOpcoes) ?? new();

            try
            {
                var respStatus = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/marketplace/status");
                if (respStatus.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await respStatus.Content.ReadAsStringAsync());
                    var ml = doc.RootElement.GetProperty("mercadoLivre").GetProperty("lojasConectadas").GetInt32();
                    LojasConectadasTexto = ml == 1 ? "1 loja conectada (Mercado Livre)" : $"{ml} lojas conectadas (Mercado Livre)";
                }
            }
            catch { LojasConectadasTexto = "Não foi possível ler o status das lojas."; }

            RecalcularResumos();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao carregar dashboard: {ex.Message}", "Marketplace", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void RecalcularResumos()
    {
        var corte = PeriodoSelecionado switch
        {
            "Hoje"    => DateTime.Today,
            "7 dias"  => DateTime.Today.AddDays(-7),
            "30 dias" => DateTime.Today.AddDays(-30),
            _         => DateTime.MinValue
        };

        var pedidosNoPeriodo = _todosPedidos.Where(p => p.DataPedidoExterno >= corte).ToList();

        TotalPedidos     = pedidosNoPeriodo.Count;
        FaturamentoTotal = pedidosNoPeriodo.Sum(p => p.ValorTotal);

        // "Precisa de atenção" NÃO é filtrado por período — um pedido travado
        // de duas semanas atrás continua precisando de atenção hoje. Fica no
        // topo de propósito (prioridade confirmada com o dono do negócio).
        var atencao = _todosPedidos
            .Where(p => p.InternalStatus == ExternalOrderStatus.AguardandoSku || p.InternalStatus == ExternalOrderStatus.ConflitoAberto)
            .OrderByDescending(p => p.DataPedidoExterno)
            .ToList();
        PrecisaAtencao.Clear();
        foreach (var p in atencao) PrecisaAtencao.Add(p);
        PedidosAtencao = atencao.Count;

        // Estoque Reservado também foge do filtro de período — é sobre o que
        // está preso AGORA, não sobre um recorte de tempo passado.
        EstoqueReservadoQtd = _todosPedidos.Count(p => p.InternalStatus == ExternalOrderStatus.EstoqueReservado);

        StatusBreakdown.Clear();
        void Add(ExternalOrderStatus status, string rotulo, string cor)
        {
            var qtd = pedidosNoPeriodo.Count(p => p.InternalStatus == status);
            if (qtd > 0) StatusBreakdown.Add(new StatusResumo(rotulo, qtd, cor));
        }
        Add(ExternalOrderStatus.Recebido,         "📥 Recebido",              "#64748B");
        Add(ExternalOrderStatus.AguardandoSku,    "⚠️ Aguardando SKU",        "#F59E0B");
        Add(ExternalOrderStatus.EstoqueReservado, "📦 Estoque Reservado",     "#8B5CF6");
        Add(ExternalOrderStatus.VendaGerada,      "✅ Venda Gerada",          "#22C55E");
        Add(ExternalOrderStatus.ConflitoAberto,   "🔴 Conflito Aberto",       "#EF4444");
        Add(ExternalOrderStatus.Concluido,        "🏁 Concluído",             "#16A34A");
        Add(ExternalOrderStatus.Cancelado,        "🚫 Cancelado",             "#94A3B8");

        FaturamentoPorCanal.Clear();
        foreach (var grupo in pedidosNoPeriodo.GroupBy(p => p.CanalNome).OrderByDescending(g => g.Sum(p => p.ValorTotal)))
            FaturamentoPorCanal.Add(new CanalResumo(grupo.Key, grupo.Sum(p => p.ValorTotal), grupo.Count()));
    }
}
