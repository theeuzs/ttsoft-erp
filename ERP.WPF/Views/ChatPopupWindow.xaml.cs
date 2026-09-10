using ERP.WPF.Services;
using Serilog;
using System;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.Views;

public partial class ChatPopupWindow : Window
{
    private readonly ChatService _chat;

    public ChatPopupWindow(ChatService chat)
    {
        _chat = chat;

        try { InitializeComponent(); }
        catch { return; }

        try { LstMensagens.ItemsSource = _chat.Mensagens; } catch (Exception ex) { Log.Warning(ex, "ChatPopupWindow: falha ao vincular lista de mensagens"); }

        _chat.Mensagens.CollectionChanged += (_, _) =>
        {
            try
            {
                if (LstMensagens.Items.Count > 0)
                    LstMensagens.ScrollIntoView(LstMensagens.Items[^1]);
            }
            catch (Exception ex) { Log.Debug(ex, "ChatPopupWindow: falha ao rolar até a última mensagem"); }
        };

        if (!_chat.Conectado)
        {
            // CORREÇÃO: Adeus gambiarra! Pegando o ID real da empresa do banco de dados, igual a Web faz.
            var tenantId = ERP.WPF.Services.TenantService.GetCurrentTenantId().ToString(); 
            var nome     = ERP.WPF.State.AppSession.UserName ?? "Terminal";
            
            _ = ConectarSilenciosoAsync(nome, tenantId);
        }
    }

    private async System.Threading.Tasks.Task ConectarSilenciosoAsync(string nome, string tenantId)
    {
        try { await _chat.ConectarAsync(nome, tenantId); }
        catch { /* falha silenciosa — mensagem de erro já adicionada pelo ChatService */ }
    }

    private async void BtnEnviar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var texto = TxtMensagem?.Text?.Trim();
            if (string.IsNullOrEmpty(texto)) return;
            TxtMensagem!.Clear();
            await _chat.EnviarMensagemAsync(texto);
        }
        catch (Exception ex) { Log.Warning(ex, "ChatPopupWindow: falha ao enviar mensagem (botão)"); }
    }

    private async void TxtMensagem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
        {
            e.Handled = true;
            try
            {
                var texto = TxtMensagem?.Text?.Trim();
                if (!string.IsNullOrEmpty(texto))
                {
                    TxtMensagem!.Clear();
                    await _chat.EnviarMensagemAsync(texto);
                }
            }
            catch (Exception ex) { Log.Warning(ex, "ChatPopupWindow: falha ao enviar mensagem (Enter)"); }
        }
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        try { Hide(); } catch (Exception ex) { Log.Debug(ex, "ChatPopupWindow: falha ao esconder a janela"); }
    }
}