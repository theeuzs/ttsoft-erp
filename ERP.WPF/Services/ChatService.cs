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

            // S10 FIX: Obter chatToken via POST /api/auth/chat-token (JWT de 5 min).
            // Antes: passava user/tenant em query string — spoofável.
            // Agora: chatToken JWT validado pelo hub.
            var chatToken = await ObterChatTokenAsync();

            string urlCompleta;
            if (!string.IsNullOrEmpty(chatToken))
            {
                urlCompleta = $"{_apiUrl}/hubs/erp-chat?chatToken={Uri.EscapeDataString(chatToken)}";
            }
            else
            {
                // Fallback degradado: sem chatToken → hub vai rejeitar a conexão.
                // Mostra mensagem de chat offline em vez de conectar com query string spoofável.
                StatusConexao = "Offline — JWT não disponível. Faça login novamente.";
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    Mensagens.Add(new ChatMensagem
                    {
                        De = "Sistema", Hora = DateTime.Now.ToString("HH:mm"),
                        Texto = "✕ Chat indisponível: JWT não obtido. Verifique a conexão com o servidor.",
                        EhMinha = false
                    }));
                return;
            }

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
                    Texto = "✕ Sem conexão com o servidor. Tente novamente.",
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

    // S10 FIX: Obtém chatToken de curta duração (5 min) via POST /api/auth/chat-token.
    // Requer JWT em AppSession.JwtToken (obtido no login via ObterJwtDaApiAsync).
    // Retorna null se JWT indisponível ou API inacessível.
    private async Task<string?> ObterChatTokenAsync()
    {
        var jwt = ERP.WPF.State.AppSession.JwtToken;
        if (string.IsNullOrEmpty(jwt)) return null;

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

            var resp = await http.PostAsync($"{_apiUrl}/api/auth/chat-token", null);
            if (!resp.IsSuccessStatusCode) return null;

            var json      = await resp.Content.ReadAsStringAsync();
            var doc       = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("chatToken").GetString();
        }
        catch { return null; }
    }
}public class ChatMensagemPayload
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