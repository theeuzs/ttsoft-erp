using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization; 
using System.Threading.Tasks;
using System.Windows;

namespace ERP.WPF.Services;

public class ChatService
{
    private HubConnection? _connection;
    private readonly string _apiUrl;

    public ObservableCollection<ChatMensagem> Mensagens  { get; } = new();
    public event Action<ChatMensagem>?        MensagemRecebida;
    public bool   Conectado   => _connection?.State == HubConnectionState.Connected;
    public string NomeUsuario { get; private set; } = "Terminal";
    public string TenantId    { get; private set; } = string.Empty;
    public string StatusConexao { get; private set; } = "Desconectado";

    public ChatService(string apiUrl) => _apiUrl = apiUrl;

    public async Task ConectarAsync(string nomeUsuario, string tenantId)
    {
        NomeUsuario    = nomeUsuario;
        TenantId       = tenantId;
        StatusConexao  = "Conectando...";

        try
        {
            if (_connection != null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            string userSeguro = Uri.EscapeDataString(nomeUsuario);
            string tenantSeguro = Uri.EscapeDataString(tenantId);
            string urlCompleta = $"{_apiUrl}/hubs/erp-chat?user={userSeguro}&tenant={tenantSeguro}";

            _connection = new HubConnectionBuilder()
                .WithUrl(urlCompleta)
                .WithAutomaticReconnect()
                .Build();

            _connection.Reconnecting  += _ => { StatusConexao = "Reconectando..."; return Task.CompletedTask; };
            _connection.Reconnected   += _ => { StatusConexao = "Conectado";        return Task.CompletedTask; };
            _connection.Closed        += _ => { StatusConexao = "Desconectado";     return Task.CompletedTask; };

            // O RECEBIMENTO DEFINITIVO E BLINDADO
            _connection.On<ChatMensagemPayload>("MensagemChat", payload =>
            {
                try
                {
                    if (payload == null) return;

                    string de = payload.De ?? "";
                    string txt = payload.Mensagem ?? "";
                    string hr = DateTime.Now.ToString("HH:mm");

                    // O filtro do eco
                    bool enviadaPorMim = string.Equals(de, NomeUsuario, StringComparison.OrdinalIgnoreCase);
                    if (enviadaPorMim) return; 

                    var msg = new ChatMensagem { De = de, Texto = txt, Hora = hr, EhMinha = false };

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Mensagens.Add(msg);
                        MensagemRecebida?.Invoke(msg);
                    });
                }
                catch { }
            });

            await _connection.StartAsync();
            StatusConexao = "Conectado";

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                Mensagens.Add(new ChatMensagem
                {
                    De = "Sistema", Hora = DateTime.Now.ToString("HH:mm"),
                    Texto = $"✅ Conectado como {NomeUsuario}. Todos os terminais online podem se comunicar.",
                    EhMinha = false 
                }));
        }
        catch (Exception ex)
        {
            StatusConexao = "Offline (API indisponível)";
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                Mensagens.Add(new ChatMensagem
                {
                    De = "Sistema", Hora = DateTime.Now.ToString("HH:mm"),
                    Texto = $"⚠️ Chat offline: {ex.Message.Split('\n')[0]}\nVerifique se a API está publicada com a versão mais recente.",
                    EhMinha = false
                }));
        }
    }

    public async Task EnviarMensagemAsync(string mensagem)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                Mensagens.Add(new ChatMensagem
                {
                    De = "Sistema", Hora = DateTime.Now.ToString("HH:mm"),
                    Texto = "❌ Sem conexão com o servidor. Faça um git push para publicar o chat na API.",
                    EhMinha = false 
                }));
            return;
        }

        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                Mensagens.Add(new ChatMensagem
                {
                    De = "Você",
                    Texto = mensagem,
                    Hora = DateTime.Now.ToString("HH:mm"),
                    EhMinha = true 
                }));

            // 👇 A CHAVE MESTRA: Agora só manda a mensagem, e a Azure resolve o resto! 👇
            await _connection.InvokeAsync("EnviarMensagemChatWpf", mensagem);
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                Mensagens.Add(new ChatMensagem
                {
                    De = "Sistema", Hora = DateTime.Now.ToString("HH:mm"),
                    Texto = $"❌ Erro ao enviar: {ex.Message.Split('\n')[0]}",
                    EhMinha = false 
                }));
        }
    }

    public async Task DesconectarAsync()
    {
        if (_connection != null)
        {
            try { await _connection.StopAsync(); } catch { }
        }
    }
}

public class ChatMensagemPayload
{
    [JsonPropertyName("de")]
    public string De { get; set; } = string.Empty;

    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("hora")]
    public string Hora { get; set; } = string.Empty;
}

public class ChatMensagem
{
    public string De      { get; set; } = string.Empty;
    public string Texto   { get; set; } = string.Empty;
    public string Hora    { get; set; } = string.Empty;
    public bool   EhMinha { get; set; }
}