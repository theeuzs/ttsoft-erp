using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

// DTO apenas para a tela desenhar a tabela
public class NotaFiscalExibicao : BaseViewModel
{
    public string NumeroRef { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Status { get; set; } = string.Empty; 
    public string Ambiente { get; set; } = string.Empty;
    public string UrlDanfe { get; set; } = string.Empty;
}

public class NotasFiscaisViewModel : BaseViewModel
{
    private readonly ISaleService _saleServiceFallback;
    private ObservableCollection<NotaFiscalExibicao> _notas = new();

    public ObservableCollection<NotaFiscalExibicao> Notas 
    { 
        get => _notas; 
        set => SetProperty(ref _notas, value); 
    }

    public ICommand AbrirPdfCommand { get; }
    public ICommand CancelarNotaCommand { get; }
    public ICommand VerMotivoCommand { get; }
    public ICommand AtualizarListaCommand { get; }

    // Construtor com Injeção de Dependência
    public NotasFiscaisViewModel(ISaleService saleService)
    {
        _saleServiceFallback = saleService;
        
        AbrirPdfCommand = new ERP.WPF.Commands.RelayCommand(AbrirPdf);
        CancelarNotaCommand = new ERP.WPF.Commands.AsyncRelayCommand(CancelarNota);
        AtualizarListaCommand = new ERP.WPF.Commands.RelayCommand(async (_) => await CarregarNotasReaisAsync());
        VerMotivoCommand = new ERP.WPF.Commands.AsyncRelayCommand(VerMotivo);

        // Carrega os dados do banco assim que a tela abre
        _ = CarregarNotasReaisAsync(); 
    }

    // A mesma mágica brilhante que você usou no SaleViewModel para evitar erros no banco!
    private async Task<T> ExecuteWithFreshSaleServiceAsync<T>(Func<ISaleService, Task<T>> action)
    {
        // Cria um "túnel" novo e isolado para o banco de dados toda vez que for chamado
        using (var scope = ERP.WPF.App.Services.CreateScope())
        {
            var freshService = scope.ServiceProvider.GetRequiredService<ISaleService>();
            return await action(freshService);
        }
    }

    private async Task CarregarNotasReaisAsync()
    {
        try
        {
            var todasVendas = await ExecuteWithFreshSaleServiceAsync(s => s.GetAllAsync());
            if (todasVendas == null) return;

            var vendasComNota = todasVendas
                .Where(v => !string.IsNullOrEmpty(v.NfceUrlDanfe) || !string.IsNullOrEmpty(v.NfceReferencia))
                .OrderByDescending(v => v.SaleDate)
                .ToList();

            var notasProcessando = vendasComNota.Where(v => v.NfceStatusFocus == "Processando").ToList();
            
            if (notasProcessando.Any())
            {
                var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
                var statusService = ERP.WPF.App.Services.GetRequiredService<INfeStatusService>();

                foreach (var pendente in notasProcessando)
                {
                    var (sucesso, statusSefaz, urlDanfe) = await statusService.ConsultarStatusNotaAsync(pendente.NfceReferencia, config.TokenFocusNfe, config.UsarAmbienteProducao);
                    
                    if (sucesso && statusSefaz != "processando_autorizacao")
                    {
                        string statusFinal = statusSefaz == "autorizado" ? "Autorizada" : statusSefaz == "cancelado" ? "Cancelada" : "Rejeitada";

                        await ExecuteWithFreshSaleServiceAsync(async s => 
                        {
                            await s.AtualizarDadosNfceAsync(pendente.Id, urlDanfe, statusFinal, pendente.NfceAmbiente, pendente.NfceReferencia);
                            return true;
                        });

                        var index = vendasComNota.IndexOf(pendente);
                        if (index >= 0)
                        {
                          vendasComNota[index] = pendente with { NfceStatusFocus = statusFinal, NfceUrlDanfe = urlDanfe };
                        }
                    }
                }
            }

            // 2. PROTEÇÃO DE THREAD: O WPF exige que alterações visuais sejam feitas na Thread principal
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                Notas.Clear();
                foreach (var v in vendasComNota)
                {
                    Notas.Add(new NotaFiscalExibicao 
                    { 
                        NumeroRef = v.NfceReferencia ?? v.SaleNumber ?? "", 
                        Data = v.SaleDate.ToString("dd/MM/yyyy HH:mm"), 
                        Cliente = string.IsNullOrWhiteSpace(v.CustomerName) ? "Consumidor Final" : v.CustomerName, 
                        Valor = v.Total, 
                        Status = v.NfceStatusFocus ?? "Autorizada", 
                        Ambiente = v.NfceAmbiente ?? "Homologação", 
                        UrlDanfe = v.NfceUrlDanfe ?? "" 
                    });
                }
            });
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                MessageBox.Show($"Erro ao carregar o histórico de notas:\n{ex.Message}", "Erro no Banco", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    private void AbrirPdf(object? obj)
    {
        if (obj is string url && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show("Não foi possível abrir o navegador para exibir a nota.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task CancelarNota(object? obj)
    {
        if (obj is not string refNota) return;

        var notaSelecionada = Notas.FirstOrDefault(n => n.NumeroRef == refNota);
        if (notaSelecionada == null) return;

        if (notaSelecionada.Status == "Cancelada")
        {
            MessageBox.Show("Esta nota já consta como Cancelada!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 👇 AQUI ENTRA A SEGURANÇA (O Bouncer) 👇
        if (!ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.NotasFiscais))
        {
            var telaSenha = new ERP.WPF.Views.SenhaGerenteView();
            telaSenha.Owner = System.Windows.Application.Current.MainWindow;
            telaSenha.ShowDialog();

            // Usuário sem permissão: pede autorização de um supervisor/gerente
            if (!telaSenha.Autorizado) return; 
        }
        // 👆 FIM DA SEGURANÇA 👆

        var confirmacao = MessageBox.Show(
            $"ATENÇÃO: Você está prestes a cancelar a nota fiscal na SEFAZ.\n" +
            $"Valor: {notaSelecionada.Valor:C}\n\n" +
            "Deseja realmente enviar o pedido de cancelamento para a Receita Federal?", 
            "Confirmar Cancelamento Fiscal",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            string justificativa = "Cancelamento solicitado pelo cliente apos a emissao."; 
            var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
            // 👇 Pega o Exterminador (Cancelamento Service)
            var cancelService = ERP.WPF.App.Services.GetRequiredService<INfeCancellationService>();

            var (sucesso, mensagem) = await cancelService.CancelarNotaAsync(refNota, justificativa, config.TokenFocusNfe, config.UsarAmbienteProducao);

            if (sucesso)
            {
                if (Guid.TryParse(refNota, out Guid vendaId))
                {
                    await ExecuteWithFreshSaleServiceAsync(async s => 
                    {
                        await s.AtualizarDadosNfceAsync(vendaId, notaSelecionada.UrlDanfe, "Cancelada", notaSelecionada.Ambiente, refNota);
                        return true;
                    });
                }
                await CarregarNotasReaisAsync(); 
                MessageBox.Show($"✅ {mensagem}\n\n⚠️ IMPORTANTE: A nota foi cancelada na SEFAZ.\nPara devolver o produto ao estoque e estornar o dinheiro do caixa, vá na tela de Vendas (F5) e cancele o pedido lá também!", 
                    "Sucesso Fiscal", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"❌ Falha ao cancelar na SEFAZ:\n{mensagem}", "Rejeição Sefaz", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao tentar cancelar:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task VerMotivo(object? obj)
    {
        if (obj is not string refNota) return;

        var notaSelecionada = Notas.FirstOrDefault(n => n.NumeroRef == refNota);
        if (notaSelecionada == null) return;

        try
        {
            var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
            // 👇 Pega o Fofoqueiro da Sefaz
            var statusService = ERP.WPF.App.Services.GetRequiredService<INfeStatusService>();

            string motivo = await statusService.ConsultarMotivoRejeicaoAsync(refNota, config.TokenFocusNfe, config.UsarAmbienteProducao);

            MessageBox.Show($"O motivo da rejeição foi:\n\n{motivo}\n\n💡 Dica: Corrija os dados no cadastro do cliente (F4) ou do produto (F3), vá no PDV e tente emitir a nota novamente.", 
                "Detetive SEFAZ", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao consultar motivo:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}