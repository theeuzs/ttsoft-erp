using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CaixaController : ControllerBase
{
    private readonly ICaixaService   _service;
    private readonly IRequestTenant  _tenant;
    private readonly IConfiguration  _config;
    private readonly AppDbContext    _db;

    public CaixaController(ICaixaService service, IRequestTenant tenant, IConfiguration config, AppDbContext db)
    {
        _service = service;
        _tenant  = tenant;
        _config  = config;
        _db      = db;
    }

    private Guid UsuarioId => Guid.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? Guid.Empty.ToString());

    /// <summary>Retorna o caixa aberto do usuário autenticado.</summary>
    [HasPermission(Permissions.CashViewSummary)]
    [HttpGet("aberto")]
    public async Task<IActionResult> GetAberto()
    {
        var caixa = await _service.ObterCaixaAbertoAsync(UsuarioId);
        return caixa is null ? NotFound(new { mensagem = "Nenhum caixa aberto." }) : Ok(caixa);
    }

    /// <summary>
    /// Fase C (módulo Caixa) — resumo/extrato agregado pra uma data (hoje ou
    /// passada). Agregação inteira feita no servidor — ver
    /// CaixaService.ObterResumoAsync. `data` no formato yyyy-MM-dd; omitido
    /// = hoje.
    /// </summary>
    [HasPermission(Permissions.CashViewSummary)]
    [HttpGet("resumo")]
    public async Task<IActionResult> GetResumo([FromQuery] DateTime? data = null)
    {
        var resumo = await _service.ObterResumoAsync(
            UsuarioId, data ?? ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil().Date);
        return resumo is null
            ? NotFound(new { mensagem = "Nenhum caixa encontrado para essa data." })
            : Ok(resumo);
    }

    /// <summary>Abre um novo caixa para o usuário autenticado.</summary>
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir([FromBody] AbrirCaixaRequestDto dto)
    {
        // S8 FIX: UsuarioId e OperadorNome do JWT — não confiar no body.
        // Antes: dto.UsuarioId do body permitia abrir caixa em nome de outro usuário (DoS + audit trail falso).
        var operadorNome = User.FindFirst(ClaimTypes.Name)?.Value
                        ?? User.FindFirst("name")?.Value
                        ?? "Operador";
        try
        {
            await _service.AbrirCaixaAsync(new AbrirCaixaDto
            {
                UsuarioId     = UsuarioId,    // ← JWT
                OperadorNome  = operadorNome, // ← JWT
                ValorAbertura = dto.ValorAbertura
            });
            return Ok(new { mensagem = "Caixa aberto com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Fecha o caixa do usuário autenticado.</summary>
    [HttpPost("fechar")]
    public async Task<IActionResult> Fechar()
    {
        try
        {
            await _service.FecharCaixaAsync(UsuarioId);
            return Ok(new { mensagem = "Caixa fechado com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Registra sangria no caixa.</summary>
    [HttpPost("sangria")]
    public async Task<IActionResult> Sangria([FromBody] MovimentoCaixaRequest dto)
    {
        if (!await TemPermissaoOuAutorizacaoAsync(Permissions.CashSangria, dto.AutorizadorToken))
            return Forbid();

        try
        {
            // S13: passa MaxSangriaValue do cargo (IRequestTenant) para SangriaPolicy
            await _service.RegistrarMovimentoAsync(
                UsuarioId, dto.Valor, dto.Descricao,
                PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria,
                maxSangriaValue: _tenant.MaxSangriaValue);
            return Ok(new { mensagem = "Sangria registrada." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Registra suprimento no caixa.</summary>
    [HttpPost("suprimento")]
    public async Task<IActionResult> Suprimento([FromBody] MovimentoCaixaRequest dto)
    {
        if (!await TemPermissaoOuAutorizacaoAsync(Permissions.CashSangria, dto.AutorizadorToken))
            return Forbid();

        try
        {
            await _service.RegistrarMovimentoAsync(
                UsuarioId, dto.Valor, dto.Descricao,
                PaymentMethod.Dinheiro, TipoMovimentoCaixa.Suprimento);
            return Ok(new { mensagem = "Suprimento registrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Registra movimento genérico no caixa.</summary>
    [HttpPost("movimento")]
    public async Task<IActionResult> Movimento([FromBody] MovimentoCaixaRequest dto)
    {
        if (!Enum.TryParse<TipoMovimentoCaixa>(dto.Tipo, out var tipo))
            return BadRequest(new { erro = $"Tipo inválido: {dto.Tipo}" });

        if (!Enum.TryParse<PaymentMethod>(dto.FormaPagamento ?? "Dinheiro", out var forma))
            forma = PaymentMethod.Dinheiro;

        // S8 FIX: tipos restritos exigem a mesma permissão que os endpoints dedicados.
        // Antes: POST /movimento?Tipo=Sangria bypass total de [HasPermission(CashSangria)] em /sangria.
        var tipoRestrito = tipo is TipoMovimentoCaixa.Sangria or TipoMovimentoCaixa.Suprimento;
        if (tipoRestrito && !await TemPermissaoOuAutorizacaoAsync(Permissions.CashSangria, dto.AutorizadorToken))
            return Forbid();

        try
        {
            await _service.RegistrarMovimentoAsync(UsuarioId, dto.Valor, dto.Descricao, forma, tipo);
            return Ok(new { mensagem = "Movimento registrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>
    /// Fase C (achado testando, não na auditoria original) — checagem de
    /// idempotência financeira usada por MotorFinanceiroService, que roda
    /// TANTO no servidor (SalesController, ao criar a venda) QUANTO no WPF
    /// (FinalizarVendaViewModel/SaleViewModel, na hora da venda — inclusive
    /// offline, pra atualizar a gaveta na hora). Sem [HasPermission] extra:
    /// não é uma consulta de resumo, é uma pergunta de "já registrei isso?"
    /// que qualquer venda precisa fazer, de qualquer operador autenticado.
    /// </summary>
    [HttpGet("existe-movimento/{salePaymentId:guid}")]
    public async Task<IActionResult> ExisteMovimentoParaSalePayment(Guid salePaymentId)
        => Ok(await _service.ExisteMovimentoParaSalePaymentAsync(salePaymentId));

    /// <summary>
    /// Fase C (achado testando) — checa se QUEM ESTÁ FAZENDO a chamada tem a
    /// permissão direto, OU se um SEGUNDO usuário (gerente/admin) autorizou
    /// esta operação especificamente, provando isso com o próprio token dele.
    ///
    /// Por que não trocar o Bearer da chamada pelo token do autorizador
    /// (jeito mais óbvio, tentado e revertido): UsuarioId acima vem SEMPRE
    /// do token que autentica a chamada (S8 FIX, de propósito — impede um
    /// usuário mexer no caixa de outro). Se a chamada inteira fosse feita
    /// com o token do gerente, UsuarioId viraria o ID DELE — a sangria/
    /// suprimento cairia no caixa do gerente (ou falharia, se ele não tiver
    /// caixa aberto — foi exatamente o erro visto testando). Por isso o
    /// token do autorizador vem SEPARADO, no corpo do request, e é validado
    /// aqui de forma independente — sem nunca virar "quem" fez a chamada.
    ///
    /// Validação replica o que o pipeline de autenticação normal já faz
    /// (Program.cs, AddJwtBearer): assinatura, emissor, audiência, validade
    /// — E a MESMA checagem de token_version (revogação de sessão: logout/
    /// troca de senha/desativação invalidam tokens antigos mesmo não
    /// expirados). Sem isso, um gerente que perdeu acesso recentemente
    /// (mas cujo token JWT ainda não expirou) continuaria conseguindo
    /// autorizar operações por aqui mesmo depois de "desligado" do sistema.
    /// Cross-tenant também é checado — token de autorizador de OUTRO tenant
    /// nunca é aceito, mesmo criptograficamente válido.
    /// </summary>
    private async Task<bool> TemPermissaoOuAutorizacaoAsync(string permissao, string? autorizadorToken)
    {
        if (User.HasClaim("permission", permissao))
            return true;

        if (string.IsNullOrWhiteSpace(autorizadorToken))
            return false;

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var parametros = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = _config["Jwt:Issuer"],
                ValidAudience            = _config["Jwt:Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)),
                ClockSkew                = TimeSpan.Zero
            };
            principal = handler.ValidateToken(autorizadorToken, parametros, out _);
        }
        catch
        {
            // Assinatura inválida, expirado, emissor/audiência errados, etc.
            // — token do autorizador não presta, não autoriza nada.
            return false;
        }

        // Mesmo tenant do usuário que está fazendo a chamada — nunca aceita
        // autorizador de outro tenant, por mais válido que o token seja.
        var tenantIdClaim = principal.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tenantIdAutorizador) || tenantIdAutorizador != _tenant.TenantId)
            return false;

        // Mesma checagem de revogação de sessão que o pipeline normal faz —
        // ver OnTokenValidated em Program.cs. Sem isso, um token de gerente
        // já deslogado/desativado (mas ainda não expirado) continuaria
        // valendo pra autorizar por aqui.
        var userIdClaim       = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var tokenVersionClaim = principal.FindFirst("token_version")?.Value;
        if (Guid.TryParse(userIdClaim, out var autorizadorId) && int.TryParse(tokenVersionClaim, out var tokenVersion))
        {
            var atual = await _db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == autorizadorId)
                .Select(u => new { u.TokenVersion, u.IsActive })
                .FirstOrDefaultAsync();

            if (atual == null || !atual.IsActive || atual.TokenVersion != tokenVersion)
                return false;
        }

        return principal.HasClaim("permission", permissao);
    }
}

public record MovimentoCaixaRequest(
    decimal Valor,
    string  Descricao,
    string? Tipo             = "Sangria",
    string? FormaPagamento   = "Dinheiro",
    string? AutorizadorToken = null);