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
    public ICommand ExportarQuarentenaCommand { get; }
    public ICommand ReenfileirarQuarentenaCommand { get; }

    public SincronizacaoDiagnosticoViewModel()
    {
        ExportarQuarentenaCommand      = new AsyncRelayCommand(async _ => await ExportarQuarentenaAsync());
        ReenfileirarQuarentenaCommand  = new AsyncRelayCommand(async _ => await ReenfileirarQuarentenaAsync());
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
            var offlineDb = App.Services.GetRequiredService<OfflineSyncService>();
            var engine    = App.Services.GetRequiredService<SyncEngineService>();
            var sincronizados = await engine.ProcessarOutboxAsync();

            var status = await offlineDb.GetStatusAsync();
            ConnectivityIndicatorState.AtualizarAposCiclo(true, status.VendasNaoSincronizadas);

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
            var offlineDb = App.Services.GetRequiredService<OfflineSyncService>();
            var engine    = App.Services.GetRequiredService<SyncEngineService>();
            bool alcancouRede = await engine.SincronizarCatalogoAsync();

            var status = await offlineDb.GetStatusAsync();
            // Achado real do teste manual (08/2026) — clicar aqui não
            // atualizava o indicador do PDV, só o ciclo automático do
            // MainWindow fazia isso. O indicador ficava parado no último
            // resultado do timer, ignorando o clique manual.
            ConnectivityIndicatorState.AtualizarAposCiclo(alcancouRede, status.VendasNaoSincronizadas);

            ResultadoTexto = alcancouRede
                ? $"Catálogo sincronizado às {DateTime.Now:HH:mm:ss}."
                : $"Falha ao alcançar o servidor às {DateTime.Now:HH:mm:ss} — provavelmente offline.";
        }
        catch (Exception ex)
        {
            ResultadoTexto = $"Erro ao sincronizar catálogo: {ex.Message}";
            ConnectivityIndicatorState.AtualizarAposCiclo(false, 0);
        }
        finally
        {
            Processando = false;
            await AtualizarStatusAsync();
        }
    }

    private async Task ExportarQuarentenaAsync()
    {
        try
        {
            var offlineDb = App.Services.GetRequiredService<OfflineSyncService>();
            var vendas = await offlineDb.GetVendasEmQuarentenaAsync();
            if (vendas.Count == 0) { ResultadoTexto = "Nenhuma venda em quarentena."; return; }

            // Só leitura da fila: cria arquivos novos, não altera nem apaga nada.
            var pasta = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TTSoft.ERP", "quarentena");
            System.IO.Directory.CreateDirectory(pasta);

            foreach (var (id, erro, criadoEm, json) in vendas)
            {
                var arquivo = System.IO.Path.Combine(pasta, $"venda_{id}.json");
                var conteudo = System.Text.Json.JsonSerializer.Serialize(
                    new { VendaId = id, CriadoEm = criadoEm, MotivoDaRecusa = erro, Payload = System.Text.Json.JsonDocument.Parse(json).RootElement },
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(arquivo, conteudo);
            }
            ResultadoTexto = $"{vendas.Count} venda(s) exportada(s) para {pasta}";
        }
        catch (Exception ex) { ResultadoTexto = $"Erro ao exportar: {ex.Message}"; }
    }

    private async Task ReenfileirarQuarentenaAsync()
    {
        try
        {
            var offlineDb = App.Services.GetRequiredService<OfflineSyncService>();
            var n = await offlineDb.ReenfileirarQuarentenaAsync();
            ResultadoTexto = n == 0 ? "Nenhuma venda em quarentena." : $"{n} venda(s) devolvida(s) à fila — rode \"Processar Outbox Agora\".";
        }
        catch (Exception ex) { ResultadoTexto = $"Erro ao reenfileirar: {ex.Message}"; }
        finally { await AtualizarStatusAsync(); }
    }

    private async Task AtualizarStatusAsync()
    {
        try
        {
            var offlineDb = App.Services.GetRequiredService<OfflineSyncService>();
            var status = await offlineDb.GetStatusAsync();
            StatusOfflineDb =
                $"Vendas pendentes: {status.VendasPendentes}  |  Com erro: {status.VendasComErro}  |  Em quarentena (regra de negócio): {status.VendasEmQuarentena}\n" +
                $"Produtos em cache: {status.TotalProdutos}  |  Clientes em cache: {status.TotalClientes}\n" +
                $"Banco local: {status.TamanhoBancoFormatado}  ({status.CaminhoBanco})";
        }
        catch (Exception ex)
        {
            StatusOfflineDb = $"Erro ao ler status: {ex.Message}";
        }
    }
}