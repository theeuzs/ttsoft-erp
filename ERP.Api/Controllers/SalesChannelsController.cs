// ── ERP.Api/Controllers/SalesChannelsController.cs ─────────────────────────
using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// Endpoint genérico de canais de venda — um único ponto de entrada pra
/// conectar qualquer marketplace, em vez de uma rota por plataforma
/// (POST /api/marketplace/ml/..., /shopee/..., etc.). O front (WPF) só
/// precisa saber o "Tipo" (enum) e chamar sempre o mesmo endpoint; a lógica
/// de qual fluxo de autorização usar fica escondida aqui dentro.
/// </summary>
[ApiController]
[Route("api/saleschannels")]
public class SalesChannelsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IRequestTenant _tenant;
    private readonly IMercadoLivreAuthService _mlAuth;

    public SalesChannelsController(IUnitOfWork uow, IRequestTenant tenant, IMercadoLivreAuthService mlAuth)
    {
        _uow    = uow;
        _tenant = tenant;
        _mlAuth = mlAuth;
    }

    /// <summary>
    /// Cria um novo canal pro tenant atual e devolve a URL de autorização
    /// (quando o tipo já tem um fluxo de auto-atendimento implementado).
    /// O front só precisa abrir essa URL no navegador — nada de SQL, GUID
    /// copiado manualmente, ou acesso ao Azure.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> Criar([FromBody] CriarSalesChannelDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest("Nome do canal é obrigatório.");

        // Reaproveita um canal do mesmo tipo que já existe mas ainda não foi
        // autorizado, em vez de criar outro — evita acumular canais órfãos se
        // o lojista clicar "Conectar" mais de uma vez antes de terminar a
        // autorização (ex: navegador não abriu, tela travou, etc.).
        var existentes = await _uow.OrderSync.GetCanaisAtivosAsync();
        var pendente = existentes.FirstOrDefault(c => c.Tipo == dto.Tipo && string.IsNullOrEmpty(c.AccessToken));

        SalesChannel canal;
        if (pendente is not null)
        {
            canal = pendente;
        }
        else
        {
            canal = new SalesChannel
            {
                Tipo                = dto.Tipo,
                Nome                = dto.Nome,
                IsAtivo             = dto.Ativo,
                // RecebePedidos é a única capacidade com dispatcher de verdade hoje
                // (AtualizarStatusPedidoAsync/SincronizarEstoqueAsync ainda são stub).
                Capacidades         = ChannelCapability.RecebePedidos,
                UsuarioIntegracaoId = _tenant.UserId == Guid.Empty ? null : _tenant.UserId,
            };

            canal = await _uow.OrderSync.AdicionarCanalAsync(canal);
        }

        string? authorizationUrl = dto.Tipo switch
        {
            SalesChannelType.MercadoLivre => _mlAuth.ObterUrlAutorizacao(canal.Id),
            _                             => null, // Shopee/outros: sem auto-atendimento ainda
        };

        return Ok(new SalesChannelCriadoDto(canal.Id, authorizationUrl));
    }

    /// <summary>
    /// Lista os canais do tenant atual com o suficiente pra tela de
    /// Integrações renderizar o card de status de cada um sem lógica extra.
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> Listar()
    {
        var canais = await _uow.OrderSync.GetCanaisAtivosAsync();
        var resultado = new List<SalesChannelStatusDto>();

        foreach (var canal in canais)
        {
            var ultimaSessao = await _uow.OrderSync.GetUltimaSessaoAsync(canal.Id);
            resultado.Add(new SalesChannelStatusDto(
                canal.Id,
                canal.Tipo,
                canal.Nome,
                Conectado: !string.IsNullOrEmpty(canal.AccessToken),
                ExternalAccountId: canal.ExternalAccountId,
                TokenExpiraEm: canal.TokenExpiraEm,
                TokenValido: canal.TokenExpiraEm is not null && canal.TokenExpiraEm > DateTime.UtcNow,
                UltimaSincronizacao: ultimaSessao?.FinalizadoEm,
                UltimoTotalProcessados: ultimaSessao?.TotalPedidosProcessados));
        }

        return Ok(resultado);
    }
}