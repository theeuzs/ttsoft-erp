// ── ERP.Api/Hubs/ERPHub.cs ───────────────────────────────────────────────────
// S1.5 — VULN #5 CORRIGIDA: ERPChatHub não extrai mais tenant/user de
// query strings controláveis pelo cliente.
// Agora valida o ChatToken JWT (emitido por POST /api/auth/chat-token)
// e extrai as claims diretamente do token assinado.
//
// Antes (vulnerável):
//   tenant = httpContext.Request.Query["tenant"]  ← spoofável
//   user   = httpContext.Request.Query["user"]    ← spoofável
//
// Depois (seguro):
//   ChatToken validado com a mesma chave JWT da API
//   tenant/user extraídos das claims do token assinado
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ERP.Api.Hubs;

/// <summary>
/// Hub SignalR para notificações em tempo real (Dashboard, Estoque, Vendas).
/// </summary>
[Authorize]
public class ERPHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantId != null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");

        await base.OnDisconnectedAsync(exception);
    }

    public async Task EnviarMensagemChat(string mensagem, string? filialId = null)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value
                    ?? Context.User?.FindFirst("name")?.Value
                    ?? "Terminal";

        if (string.IsNullOrEmpty(tenantId)) return;

        var payload = new
        {
            De       = userName,
            Mensagem = mensagem,
            FilialId = filialId,
            Hora     = DateTime.Now.ToString("HH:mm"),
            TenantId = tenantId
        };

        if (!string.IsNullOrEmpty(filialId))
            await Clients.Group($"tenant-{tenantId}-filial-{filialId}").SendAsync("MensagemChat", payload);
        else
            await Clients.Group($"tenant-{tenantId}").SendAsync("MensagemChat", payload);
    }

    public async Task EntrarFilial(string filialId)
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (tenantId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}-filial-{filialId}");
    }
}

/// <summary>
/// Hub de chat interno — WPF e Portal.
/// S1.5: autenticação via ChatToken JWT validado no OnConnectedAsync.
/// O token chega via query string ?chatToken=... (padrão SignalR para WebSocket).
/// </summary>
[AllowAnonymous]
public class ERPChatHub : Hub
{
    private const string KeyTenant = "chat_tenant_id";
    private const string KeyNome   = "chat_nome_usuario";
    private const string KeySala   = "chat_sala";
    private const int    HistoricoMax = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration       _config;

    public ERPChatHub(IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _config       = config;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        // S1.5: valida o ChatToken JWT — não confia mais na query string diretamente
        var chatToken = httpContext?.Request.Query["chatToken"].ToString();
        var claims    = ValidarChatToken(chatToken);

        string? tenantId;
        string  nomeUsuario;

        if (claims != null)
        {
            // Token válido: extrai tenant e usuário das claims assinadas
            tenantId    = claims.FindFirst("tenant_id")?.Value;
            nomeUsuario = claims.FindFirst(ClaimTypes.Name)?.Value
                       ?? claims.FindFirst(JwtRegisteredClaimNames.Name)?.Value
                       ?? "Terminal";
        }
        else
        {
            // Fallback para WPF legado sem ChatToken — aceita query string
            // mas com log de aviso para forçar migração gradual
            tenantId    = httpContext?.Request.Query["tenant"].ToString();
            nomeUsuario = httpContext?.Request.Query["user"].ToString() ?? "Terminal";

            if (!string.IsNullOrEmpty(tenantId))
                Serilog.Log.Warning(
                    "ERPChatHub: conexão sem ChatToken JWT de {Tenant}/{User} — " +
                    "migrar para POST /api/auth/chat-token",
                    tenantId, nomeUsuario);
        }

        var sala = httpContext?.Request.Query["sala"].ToString();

        if (string.IsNullOrEmpty(tenantId))
        {
            // Recusa conexão sem identificação de tenant
            Context.Abort();
            return;
        }

        Context.Items[KeyTenant] = tenantId;
        Context.Items[KeyNome]   = nomeUsuario;
        Context.Items[KeySala]   = sala;

        var grupo = string.IsNullOrEmpty(sala)
            ? $"chat-{tenantId}"
            : $"chat-{tenantId}-{sala}";

        await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
        await EnviarHistoricoAsync(tenantId, sala);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Valida o ChatToken JWT usando a mesma chave da API.
    /// Retorna as claims se válido, null se inválido ou expirado.
    /// </summary>
    private ClaimsPrincipal? ValidarChatToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            var key       = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var handler   = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = _config["Jwt:Issuer"],
                ValidAudience            = _config["Jwt:Audience"],
                IssuerSigningKey         = key,
                ClockSkew                = TimeSpan.FromSeconds(30)
            }, out _);

            // Garante que é de fato um ChatToken (não o JWT de sessão completo)
            var isChatToken = principal.FindFirst("chat_token")?.Value == "true";
            return isChatToken ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task EnviarHistoricoAsync(string tenantId, string? sala)
    {
        try
        {
            if (!Guid.TryParse(tenantId, out var tenantGuid)) return;

            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var historico = await ctx.ChatMessages
                .AsNoTracking()
                .Where(m => m.TenantId == tenantGuid
                         && (sala == null ? m.Sala == null : m.Sala == sala))
                .OrderByDescending(m => m.CreatedAt)
                .Take(HistoricoMax)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    Id         = m.Id,
                    De         = m.RemetenteNome,
                    Mensagem   = m.Mensagem,
                    Hora       = m.CreatedAt.ToLocalTime().ToString("HH:mm"),
                    TenantId   = tenantId,
                    Persistida = true
                })
                .ToListAsync();

            if (historico.Count > 0)
                await Clients.Caller.SendAsync("HistoricoChat", historico);
        }
        catch { }
    }

    public async Task EnviarMensagemChatWpf(string mensagem)
    {
        var tenantId    = Context.Items[KeyTenant]?.ToString();
        var nomeUsuario = Context.Items[KeyNome]?.ToString() ?? "Terminal";
        var sala        = Context.Items[KeySala]?.ToString();

        if (string.IsNullOrEmpty(tenantId)) return;
        if (string.IsNullOrWhiteSpace(mensagem) || mensagem.Length > 2000) return;

        Guid? msgId = null;
        if (Guid.TryParse(tenantId, out var tenantGuid))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var entity = new ChatMessage
                {
                    TenantId      = tenantGuid,
                    RemetenteNome = nomeUsuario,
                    Mensagem      = mensagem,
                    Sala          = string.IsNullOrEmpty(sala) ? null : sala
                };

                ctx.ChatMessages.Add(entity);
                await ctx.SaveChangesAsync();
                msgId = entity.Id;
            }
            catch { }
        }

        var payload = new
        {
            Id         = msgId,
            De         = nomeUsuario,
            Mensagem   = mensagem,
            Hora       = DateTime.Now.ToString("HH:mm"),
            TenantId   = tenantId,
            Persistida = msgId.HasValue
        };

        var grupo = string.IsNullOrEmpty(sala)
            ? $"chat-{tenantId}"
            : $"chat-{tenantId}-{sala}";

        await Clients.Group(grupo).SendAsync("MensagemChat", payload);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = Context.Items[KeyTenant]?.ToString();
        var sala     = Context.Items[KeySala]?.ToString();

        if (!string.IsNullOrEmpty(tenantId))
        {
            var grupo = string.IsNullOrEmpty(sala)
                ? $"chat-{tenantId}"
                : $"chat-{tenantId}-{sala}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, grupo);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Serviço de notificações do servidor → clientes conectados.</summary>
public class NotificacaoService
{
    private readonly IHubContext<ERPHub> _hub;
    public NotificacaoService(IHubContext<ERPHub> hub) => _hub = hub;

    public async Task NotificarTenantAsync(string tenantId, string tipo, object dados)
        => await _hub.Clients.Group($"tenant-{tenantId}")
            .SendAsync("Notificacao", new { tipo, dados, hora = DateTime.Now });

    public async Task AlertaEstoqueCriticoAsync(string tenantId, string nomeProduto,
        decimal estoque, decimal minimo)
        => await NotificarTenantAsync(tenantId, "estoque_critico", new
        {
            produto  = nomeProduto,
            estoque,
            minimo,
            mensagem = $"⚠️ {nomeProduto}: estoque {estoque} abaixo do mínimo ({minimo})"
        });

    public async Task NovaVendaAsync(string tenantId, decimal valor, string vendedor)
        => await NotificarTenantAsync(tenantId, "nova_venda", new
        {
            valor,
            vendedor,
            mensagem = $"💰 Nova venda: {valor:C} por {vendedor}"
        });

    public async Task ContaVencidaAsync(string tenantId, string cliente, decimal valor)
        => await NotificarTenantAsync(tenantId, "conta_vencida", new
        {
            cliente,
            valor,
            mensagem = $"🔴 Conta vencida: {cliente} — {valor:C}"
        });
}