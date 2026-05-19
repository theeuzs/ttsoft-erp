using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class NotificacaoItem
{
    public string   Tipo     { get; set; } = string.Empty;
    public string   Titulo   { get; set; } = string.Empty;
    public string   Mensagem { get; set; } = string.Empty;
    public DateTime Data     { get; set; } = DateTime.Now;
    public bool     Lida     { get; set; }

    public string Icone => Tipo switch
    {
        "estoque"    => "📦",
        "financeiro" => "💰",
        "compra"     => "🛒",
        _            => "🔔"
    };

    public string Cor => Tipo switch
    {
        "estoque"    => "#F59E0B",
        "financeiro" => "#DC2626",
        "compra"     => "#3B82F6",
        _            => "#64748B"
    };

    public string DataFormatada => Data.ToString("dd/MM HH:mm");
}

public class NotificacoesViewModel : BaseViewModel
{
    private readonly DispatcherTimer _timer;

    public ObservableCollection<NotificacaoItem> Notificacoes { get; } = new();

    private int _naoLidas;
    public int NaoLidas
    {
        get => _naoLidas;
        set { SetProperty(ref _naoLidas, value); OnPropertyChanged(nameof(TemNotificacoes)); }
    }

    public bool TemNotificacoes => NaoLidas > 0;

    public ICommand MarcarTodasLidasCommand { get; }
    public ICommand AtualizarCommand        { get; }

    public static Action<int>? OnNaoLidasChanged;

    public NotificacoesViewModel()
    {
        MarcarTodasLidasCommand = new RelayCommand(_ => MarcarTodasLidas());
        AtualizarCommand        = new AsyncRelayCommand(async _ => await VerificarNotificacoesAsync());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _timer.Tick += async (s, e) => await VerificarNotificacoesAsync();
        _timer.Start();

        _ = VerificarNotificacoesAsync();
    }

    public async Task VerificarNotificacoesAsync()
    {
        try
        {
            using var scope      = App.Services.CreateScope();
            var productService   = scope.ServiceProvider.GetRequiredService<IProductService>();
            var contaPagarSvc    = scope.ServiceProvider.GetRequiredService<IContaPagarService>();
            var contaReceberSvc  = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
            var pedidoSvc        = scope.ServiceProvider.GetRequiredService<IPedidoCompraService>();

            var novas = new List<NotificacaoItem>();

            // ── 1. Estoque baixo ──────────────────────────────────────────
            var lowStockList = await productService.GetLowStockListAsync();
            var lowStockArr  = lowStockList.ToList();
            if (lowStockArr.Any())
            {
                var nomes   = string.Join(", ", lowStockArr.Take(5).Select(p => p.Name));
                var sufixo  = lowStockArr.Count > 5 ? $" e mais {lowStockArr.Count - 5}..." : "";
                novas.Add(new NotificacaoItem
                {
                    Tipo     = "estoque",
                    Titulo   = $"Estoque Baixo — {lowStockArr.Count} produto(s)",
                    Mensagem = nomes + sufixo,
                    Data     = DateTime.Now,
                });
            }

            // ── 2. Contas a pagar vencendo hoje ───────────────────────────
            var contasVencendo = (await contaPagarSvc.GetVencendoHojeAsync()).ToList();
            if (contasVencendo.Any())
            {
                var totalValor = contasVencendo.Sum(c => c.Valor);
                var nomes      = string.Join("\n", contasVencendo.Take(5)
                                   .Select(c => $"• {c.Descricao} — R$ {c.Valor:N2}"));
                var sufixo     = contasVencendo.Count > 5 ? $"\n  e mais {contasVencendo.Count - 5}..." : "";
                novas.Add(new NotificacaoItem
                {
                    Tipo     = "financeiro",
                    Titulo   = $"Contas Vencendo Hoje — R$ {totalValor:N2}",
                    Mensagem = nomes + sufixo,
                    Data     = DateTime.Now,
                });
            }

            // ── 3. Clientes inadimplentes ─────────────────────────────────
            var vencidas = await contaReceberSvc.CountInadimplentesAsync();
            if (vencidas > 0)
                novas.Add(new NotificacaoItem
                {
                    Tipo     = "financeiro",
                    Titulo   = "Inadimplência",
                    Mensagem = $"{vencidas} cliente(s) com pagamento em atraso.",
                    Data     = DateTime.Now,
                });

            // ── 4. Pedidos de compra sem retorno há 7 dias ────────────────
            var pedidos = await pedidoSvc.GetAllAsync();
            int pedidosAtrasados = pedidos.Count(p =>
                p.Status == StatusPedidoCompra.Enviado &&
                p.DataPedido < DateTime.Today.AddDays(-7));

            if (pedidosAtrasados > 0)
                novas.Add(new NotificacaoItem
                {
                    Tipo     = "compra",
                    Titulo   = "Pedidos Sem Retorno",
                    Mensagem = $"{pedidosAtrasados} pedido(s) enviado(s) há mais de 7 dias sem recebimento.",
                    Data     = DateTime.Now,
                });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Notificacoes.Clear();
                foreach (var n in novas) Notificacoes.Add(n);
                NaoLidas = novas.Count;
                OnNaoLidasChanged?.Invoke(NaoLidas);
            });
        }
        catch { /* silencioso — notificações não podem travar o sistema */ }
    }

    private void MarcarTodasLidas()
    {
        foreach (var n in Notificacoes) n.Lida = true;
        NaoLidas = 0;
        OnNaoLidasChanged?.Invoke(0);
    }
}