// ERP.WPF/ViewModels/SincronizacaoDiagnosticoViewModel.cs
using ERP.Application.Interfaces;
using ERP.Infrastructure.Services;
using ERP.WPF.Commands;
using ERP.WPF.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Fase 2 do Offline-First — disparador MANUAL e TEMPORÁRIO do
/// SyncEngineService, só pra validar o Teste 3 do roteiro combinado com
/// GPT/Gemini (sincronização de volta depois de uma venda offline). De
/// propósito em Administração, não no PDV — o SyncEngine automático em
/// background e o indicador 🟢/🔴 são Fase 3, ainda não construídos.
/// Essa tela existe só pra não precisar de script externo enquanto isso.
/// </summary>
public class SincronizacaoDiagnosticoViewModel : BaseViewModel
{
    private string _resultadoTexto = "Nenhuma sincronização rodada ainda nessa sessão.";
    public string ResultadoTexto { get => _resultadoTexto; set => SetProperty(ref _resultadoTexto, value); }

    private bool _processando;
    public bool Processando { get => _processando; set => SetProperty(ref _processando, value); }

    private string _statusOfflineDb = string.Empty;
    public string StatusOfflineDb { get => _statusOfflineDb; set => SetProperty(ref _statusOfflineDb, value); }

    public ICommand ProcessarOutboxCommand { get; }
    public ICommand SincronizarCatalogoCommand { get; }
    public ICommand AtualizarStatusCommand { get; }

    public SincronizacaoDiagnosticoViewModel()
    {
        ProcessarOutboxCommand     = new AsyncRelayCommand(async _ => await ProcessarOutboxAsync());
        SincronizarCatalogoCommand = new AsyncRelayCommand(async _ => await SincronizarCatalogoAsync());
        AtualizarStatusCommand     = new AsyncRelayCommand(async _ => await AtualizarStatusAsync());

        _ = AtualizarStatusAsync();
    }

    private async Task ProcessarOutboxAsync()
    {
        Processando = true;
        ResultadoTexto = "Processando...";
        try
        {
            var offlineDb   = App.Services.GetRequiredService<OfflineSyncService>();
            var saleService = App.Services.GetRequiredService<ISaleService>();
            var productService  = App.Services.GetRequiredService<IProductService>();
            var customerService = App.Services.GetRequiredService<ICustomerService>();

            var engine = new SyncEngineService(offlineDb, saleService, productService, customerService);
            var sincronizados = await engine.ProcessarOutboxAsync();

            ResultadoTexto = $"{sincronizados} evento(s) sincronizado(s) às {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            ResultadoTexto = $"Erro ao processar: {ex.Message}";
        }
        finally
        {
            Processando = false;
            await AtualizarStatusAsync();
        }
    }

    private async Task SincronizarCatalogoAsync()
    {
        Processando = true;
        try
        {
            var offlineDb   = App.Services.GetRequiredService<OfflineSyncService>();
            var saleService = App.Services.GetRequiredService<ISaleService>();
            var productService  = App.Services.GetRequiredService<IProductService>();
            var customerService = App.Services.GetRequiredService<ICustomerService>();

            var engine = new SyncEngineService(offlineDb, saleService, productService, customerService);
            await engine.SincronizarCatalogoAsync();

            ResultadoTexto = $"Catálogo sincronizado às {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            ResultadoTexto = $"Erro ao sincronizar catálogo: {ex.Message}";
        }
        finally
        {
            Processando = false;
            await AtualizarStatusAsync();
        }
    }

    private async Task AtualizarStatusAsync()
    {
        try
        {
            var offlineDb = App.Services.GetRequiredService<OfflineSyncService>();
            var status = await offlineDb.GetStatusAsync();
            StatusOfflineDb =
                $"Vendas pendentes: {status.VendasPendentes}  |  Com erro: {status.VendasComErro}\n" +
                $"Produtos em cache: {status.TotalProdutos}  |  Clientes em cache: {status.TotalClientes}\n" +
                $"Banco local: {status.TamanhoBancoFormatado}  ({status.CaminhoBanco})";
        }
        catch (Exception ex)
        {
            StatusOfflineDb = $"Erro ao ler status: {ex.Message}";
        }
    }
}
