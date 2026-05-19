// ERP.WPF/ViewModels/ComissaoViewModel.cs
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using ERP.WPF.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ComissaoItem
{
    public string  Vendedor      { get; set; } = string.Empty;
    public int     QtdVendas     { get; set; }
    public decimal TotalVendido  { get; set; }
    public decimal ValorComissao { get; set; }
    public decimal Percentual    { get; set; } // Sprint G
}

public class ComissaoViewModel : BaseViewModel
{
    public ObservableCollection<ComissaoItem> ListaComissoes { get; } = new();

    private DateTime _dataInicio = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    // Percentual manual (fallback quando cargo não tem % configurado)
    private decimal _percentualComissao = 1.0m;
    public decimal PercentualComissao
    {
        get => _percentualComissao;
        set => SetProperty(ref _percentualComissao, value);
    }

    // Sprint G: toggle para usar % do cargo de cada vendedor
    private bool _usarPercentualPorCargo = true;
    public bool UsarPercentualPorCargo
    {
        get => _usarPercentualPorCargo;
        set { SetProperty(ref _usarPercentualPorCargo, value); OnPropertyChanged(nameof(UsarPercentualManual)); }
    }
    public bool UsarPercentualManual => !_usarPercentualPorCargo;

    private decimal _totalVendidoGeral;
    public decimal TotalVendidoGeral  { get => _totalVendidoGeral;  set => SetProperty(ref _totalVendidoGeral,  value); }

    private decimal _totalComissaoPagar;
    public decimal TotalComissaoPagar { get => _totalComissaoPagar; set => SetProperty(ref _totalComissaoPagar, value); }

    public ICommand CalcularCommand    { get; }
    public ICommand ExportarPdfCommand { get; }

    public ComissaoViewModel()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        CalcularCommand    = new RelayCommand(async _ => await CalcularComissoesAsync());
        ExportarPdfCommand = new RelayCommand(_ => ExportarPdf(), _ => ListaComissoes.Any());
        _ = CalcularComissoesAsync();
    }

    private async Task CalcularComissoesAsync()
    {
        IsBusy = true;
        try
        {
            ListaComissoes.Clear();
            var comissaoService = App.Services.GetRequiredService<IComissaoService>();

            if (_usarPercentualPorCargo)
            {
                // Sprint G: Sale.SellerName → User.Name → User.Role.PercentualComissao
                using var scope = App.Services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Busca todos os usuários com cargo e percentual de comissão
                var usuarios = await ctx.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .Where(u => !u.IsDeleted)
                    .ToListAsync();

                // Obtém totais por vendedor sem aplicar percentual
                var resultado = await comissaoService.CalcularAsync(DataInicio, DataFim, 0);

                foreach (var v in resultado.Vendedores)
                {
                    // Cruza SellerName com User.Name (case-insensitive)
                    var usuario = usuarios.FirstOrDefault(u =>
                        string.Equals(u.Name, v.Vendedor, StringComparison.OrdinalIgnoreCase));

                    // Usa % do cargo se configurado, senão cai no percentual manual
                    var pct = (usuario?.Role?.PercentualComissao ?? 0) > 0
                        ? usuario!.Role!.PercentualComissao
                        : PercentualComissao;

                    ListaComissoes.Add(new ComissaoItem
                    {
                        Vendedor      = v.Vendedor,
                        QtdVendas     = v.QtdVendas,
                        TotalVendido  = v.TotalVendido,
                        ValorComissao = Math.Round(v.TotalVendido * pct / 100, 2),
                        Percentual    = pct
                    });
                }
            }
            else
            {
                // Modo manual: percentual único para todos
                var resultado = await comissaoService.CalcularAsync(DataInicio, DataFim, PercentualComissao);
                foreach (var v in resultado.Vendedores)
                    ListaComissoes.Add(new ComissaoItem
                    {
                        Vendedor      = v.Vendedor,
                        QtdVendas     = v.QtdVendas,
                        TotalVendido  = v.TotalVendido,
                        ValorComissao = v.ValorComissao,
                        Percentual    = PercentualComissao
                    });
            }

            TotalVendidoGeral  = ListaComissoes.Sum(x => x.TotalVendido);
            TotalComissaoPagar = ListaComissoes.Sum(x => x.ValorComissao);
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao calcular comissões:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void ExportarPdf()
    {
        var config = ConfiguracaoService.Carregar();
        var doc    = new ComissaoPdfReport(config, DataInicio, DataFim,
            PercentualComissao, TotalVendidoGeral, TotalComissaoPagar, ListaComissoes);
        PdfReportBase.SalvarEAbrir(doc, "Comissoes");
    }
}
