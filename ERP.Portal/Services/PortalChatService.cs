// ── ERP.Portal/Services/PortalChatService.cs ─────────────────────────────────
// Sprint 3C: suporte a histórico persistido.
// Novo evento "HistoricoChat" recebido no OnConnectedAsync do hub —
// insere mensagens antigas no início da lista com marcador visual.
// ─────────────────────────────────────────────────────────────────────────────
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json.Serialization;

namespace ERP.Portal.Services;

public class PortalChatService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _apiUrl;

    public List<ChatMensagem> Mensagens   { get; } = new();
    public event Action?      OnMensagemNova;
    public bool               Conectado   => _connection?.State == HubConnectionState.Connected;
    public string             NomeUsuario { get; private set; } = "Portal";
    public string             TenantId    { get; private set; } = string.Empty;

    public PortalChatService(string apiUrl) => _apiUrl = apiUrl;

    public async Task ConectarAsync(string nomeUsuario, string tenantId, string? sala = null)
    {
        NomeUsuario = nomeUsuario;
        TenantId    = tenantId;

        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        var userSeguro   = Uri.EscapeDataString(nomeUsuario);
        var tenantSeguro = Uri.EscapeDataString(tenantId);
        var salaSegura   = sala != null ? $"&sala={Uri.EscapeDataString(sala)}" : "";
        var urlCompleta  = $"{_apiUrl}/hubs/erp-chat?user={userSeguro}&tenant={tenantSeguro}{salaSegura}";

        _connection = new HubConnectionBuilder()
            .WithUrl(urlCompleta)
            .WithAutomaticReconnect()
            .Build();

        // Mensagem nova em tempo real
        _connection.On<ChatMensagemPayload>("MensagemChat", payload =>
        {
            try
            {
                if (payload == null) return;

                var de  = payload.De ?? "";
                var txt = payload.Mensagem ?? "";
                var hr  = payload.Hora ?? DateTime.Now.ToString("HH:mm");

                // Ignora eco da própria mensagem (já adicionada otimisticamente)
                if (string.Equals(de, NomeUsuario, StringComparison.OrdinalIgnoreCase)) return;

                Mensagens.Add(new ChatMensagem
                {
                    De         = de,
                    Texto      = txt,
                    Hora       = hr,
                    EhMinha    = false,
                    Persistida = payload.Persistida
                });
                OnMensagemNova?.Invoke();
            }
            catch { }
        });

        // Sprint 3C: histórico persistido recebido ao conectar
        _connection.On<List<ChatMensagemHistorico>>("HistoricoChat", historico =>
        {
            try
            {
                if (historico == null || historico.Count == 0) return;

                // Insere separador de histórico no início
                var separador = new ChatMensagem
                {
                    De         = "sistema",
                    Texto      = $"── Histórico ({historico.Count} mensagens) ──",
                    Hora       = "",
                    EhMinha    = false,
                    IsSistema  = true,
                    Persistida = false
                };

                var msgs = historico
                    .Select(h => new ChatMensagem
                    {
                        De         = h.De,
                        Texto      = h.Mensagem,
                        Hora       = h.Hora,
                        EhMinha    = string.Equals(h.De, NomeUsuario,
                                         StringComparison.OrdinalIgnoreCase),
                        Persistida = true
                    })
                    .ToList();

                // Insere no início (histórico vem antes das mensagens ao vivo)
                Mensagens.InsertRange(0, new[] { separador }.Concat(msgs));
                OnMensagemNova?.Invoke();
            }
            catch { }
        });

        // Reconexão automática: recarrega histórico
        _connection.Reconnected += async _ =>
        {
            // O hub re-envia o histórico automaticamente no OnConnectedAsync
            await Task.CompletedTask;
        };

        try { await _connection.StartAsync(); }
        catch { }
    }

    public async Task EnviarAsync(string mensagem)
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        // Adiciona otimisticamente na própria tela
        Mensagens.Add(new ChatMensagem
        {
            De         = "Você",
            Texto      = mensagem,
            Hora       = DateTime.Now.ToString("HH:mm"),
            EhMinha    = true,
            Persistida = false // será true quando o hub confirmar via MensagemChat broadcast
        });
        OnMensagemNova?.Invoke();

        await _connection.InvokeAsync("EnviarMensagemChatWpf", mensagem);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null) await _connection.DisposeAsync();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class ChatMensagemPayload
{
    [JsonPropertyName("de")]       public string De        { get; set; } = string.Empty;
    [JsonPropertyName("mensagem")] public string Mensagem  { get; set; } = string.Empty;
    [JsonPropertyName("hora")]     public string Hora      { get; set; } = string.Empty;
    [JsonPropertyName("persistida")] public bool Persistida { get; set; }
}

public class ChatMensagemHistorico
{
    [JsonPropertyName("de")]       public string De       { get; set; } = string.Empty;
    [JsonPropertyName("mensagem")] public string Mensagem { get; set; } = string.Empty;
    [JsonPropertyName("hora")]     public string Hora     { get; set; } = string.Empty;
}

public class ChatMensagem
{
    public string De         { get; set; } = string.Empty;
    public string Texto      { get; set; } = string.Empty;
    public string Hora       { get; set; } = string.Empty;
    public bool   EhMinha    { get; set; }
    public bool   Persistida { get; set; }
    public bool   IsSistema  { get; set; }
}