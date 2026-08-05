// ERP.WPF/ViewModels/OrganizacaoVendasViewModel.cs
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public enum TipoRegistroVenda { Venda, PreVenda, Orcamento }

public class RegistroUnificado
{
    public Guid   Id       { get; set; }
    public TipoRegistroVenda Tipo { get; set; }
    public string Numero    { get; set; } = string.Empty;
    public string Cliente   { get; set; } = string.Empty;
    public DateTime Data    { get; set; }
    public decimal Total    { get; set; }
    public string Status    { get; set; } = string.Empty;

    public string TipoTexto => Tipo switch
    {
        TipoRegistroVenda.Venda     => "✅ Venda",
        TipoRegistroVenda.PreVenda  => "⏸ Pré-venda",
        TipoRegistroVenda.Orcamento => "📄 Orçamento",
        _ => "?"
    };

    public string CorTipo => Tipo switch
    {
        TipoRegistroVenda.Venda     => "#16A34A",
        TipoRegistroVenda.PreVenda  => "#F59E0B",
        TipoRegistroVenda.Orcamento => "#3B82F6",
        _ => "#64748B"
    };
}

/// <summary>
/// Item 2.7 do roadmap Comercial — vendas, pré-vendas e orçamentos numa
/// tela única, com filtros. Não cria nenhum serviço novo: combina as três
/// listagens que já existiam (ISaleService, IVendaSuspensaService,
/// IOrcamentoService), cada uma já testada e usada em telas próprias.
/// </summary>
public class OrganizacaoVendasViewModel : BaseViewModel
{
    private ObservableCollection<RegistroUnificado> _todosRegistros = new();
    public ObservableCollection<RegistroUnificado> Registros { get; } = new();

    private DateTime _dataInicio = DateTime.Today.AddDays(-30);
    public DateTime DataInicio { get => _dataInicio; set { SetProperty(ref _dataInicio, value); _ = CarregarAsync(); } }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set { SetProperty(ref _dataFim, value); _ = CarregarAsync(); } }

    private string _filtroBusca = string.Empty;
    public string FiltroBusca { get => _filtroBusca; set { SetProperty(ref _filtroBusca, value); AplicarFiltro(); } }

    public string[] TiposDisponiveis { get; } = { "Todos", "Vendas", "Pré-vendas", "Orçamentos" };

    private string _tipoSelecionado = "Todos";
    public string TipoSelecionado { get => _tipoSelecionado; set { SetProperty(ref _tipoSelecionado, value); AplicarFiltro(); } }

    public ICommand CarregarCommand { get; }

    public OrganizacaoVendasViewModel()
    {
        CarregarCommand = new RelayCommand(async _ => await CarregarAsync());
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var saleService     = App.Services.GetRequiredService<ISaleService>();
            var suspensaService  = App.Services.GetRequiredService<IVendaSuspensaService>();
            var orcamentoService = App.Services.GetRequiredService<IOrcamentoService>();

            var vendas     = await saleService.GetAllAsync(DataInicio, DataFim);
            var preVendas  = await suspensaService.ObterPendentesAsync();
            var orcamentos = await orcamentoService.ObterTodosAsync();

            var combinado = new ObservableCollection<RegistroUnificado>();

            foreach (var v in vendas)
                combinado.Add(new RegistroUnificado
                {
                    Id      = v.Id,
                    Tipo    = TipoRegistroVenda.Venda,
                    Numero  = v.SaleNumber,
                    Cliente = v.CustomerName ?? "Consumidor final",
                    Data    = v.SaleDate,
                    Total   = v.Total,
                    Status  = v.Status.ToString(),
                });

            foreach (var p in preVendas)
                combinado.Add(new RegistroUnificado
                {
                    Id      = p.Id,
                    Tipo    = TipoRegistroVenda.PreVenda,
                    Numero  = "—",
                    Cliente = p.ClienteNome,
                    Data    = p.DataSuspensao,
                    Total   = p.TotalAproximado,
                    Status  = p.EmEdicao ? $"Em edição ({p.NomeEmEdicao})" : "Aguardando",
                });

            foreach (var o in orcamentos.Where(o => o.DataEmissao >= DataInicio && o.DataEmissao <= DataFim))
                combinado.Add(new RegistroUnificado
                {
                    Id      = o.Id,
                    Tipo    = TipoRegistroVenda.Orcamento,
                    Numero  = "—",
                    Cliente = o.CustomerName ?? "Consumidor final",
                    Data    = o.DataEmissao,
                    Total   = o.ValorTotal,
                    Status  = o.Status.ToString(),
                });

            _todosRegistros = combinado;
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar organização de vendas:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        var filtrados = _todosRegistros.AsEnumerable();

        filtrados = TipoSelecionado switch
        {
            "Vendas"      => filtrados.Where(r => r.Tipo == TipoRegistroVenda.Venda),
            "Pré-vendas"  => filtrados.Where(r => r.Tipo == TipoRegistroVenda.PreVenda),
            "Orçamentos"  => filtrados.Where(r => r.Tipo == TipoRegistroVenda.Orcamento),
            _ => filtrados
        };

        if (!string.IsNullOrWhiteSpace(FiltroBusca))
            filtrados = filtrados.Where(r => r.Cliente.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase)
                                           || r.Numero.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase));

        Registros.Clear();
        foreach (var r in filtrados.OrderByDescending(r => r.Data)) Registros.Add(r);
    }
}
