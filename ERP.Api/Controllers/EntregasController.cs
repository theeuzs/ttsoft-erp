using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EntregasController : ControllerBase
{
    private readonly IEntregaService _service;
    private readonly IRequestTenant  _tenant;

    public EntregasController(IEntregaService service, IRequestTenant tenant)
    {
        _service = service;
        _tenant  = tenant;
    }

    /// <summary>
    /// Lista entregas com filtros opcionais de data, status e motorista.
    /// Padrão: entregas de hoje.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? data        = null,
        [FromQuery] string?   status      = null,
        [FromQuery] Guid?     motoristaId = null)
    {
        var entregas = await _service.GetAllAsync(data ?? DateTime.Today, status, motoristaId);
        return Ok(entregas);
    }

    /// <summary>Retorna detalhes de uma entrega específica.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var e = await _service.GetByIdAsync(id);
        return e is null ? NotFound() : Ok(e);
    }

    /// <summary>Retorna a entrega vinculada a uma venda.</summary>
    [HttpGet("por-venda/{saleId:guid}")]
    public async Task<IActionResult> GetBySale(Guid saleId)
    {
        var e = await _service.GetBySaleAsync(saleId);
        return e is null ? NotFound() : Ok(e);
    }

    /// <summary>Cria uma nova entrega para uma venda.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEntregaDto dto)
    {
        try
        {
            var entrega = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = entrega.Id }, entrega);
        }
        catch (Exception ex) { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Remove uma entrega (soft-delete).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Atualiza o status de uma entrega.
    /// Entregue → preenche DataEntrega + AssinadoPor.
    /// Reagendada → nova DataPrevista volta status para Pendente.
    /// Cancelada/Reagendada → MotivoProblema.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> AtualizarStatus(Guid id, [FromBody] AtualizarStatusEntregaDto dto)
    {
        try
        {
            var e = await _service.AtualizarStatusAsync(id, dto);
            return Ok(e);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Atribui motorista e veículo a uma entrega. Muda status para EmRota automaticamente.</summary>
    [HttpPut("{id:guid}/motorista")]
    public async Task<IActionResult> AtribuirMotorista(Guid id, [FromBody] AtribuirMotoristaDto dto)
    {
        try
        {
            var e = await _service.AtribuirMotoristaAsync(id, dto);
            return Ok(e);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Relatório de entregas do dia: total, taxas, custo e breakdown por motorista.</summary>
    [HttpGet("relatorio")]
    public async Task<IActionResult> GetRelatorio([FromQuery] DateTime? data = null)
        => Ok(await _service.GetRelatorioAsync(data ?? DateTime.Today));

    /// <summary>
    /// Gera o romaneio do dia em HTML pronto para impressão.
    /// Endpoint público de conteúdo — retorna text/html.
    /// </summary>
    [HttpGet("romaneio")]
    public async Task<IActionResult> GetRomaneio(
        [FromQuery] DateTime? data        = null,
        [FromQuery] Guid?     motoristaId = null)
    {
        var dia      = data ?? DateTime.Today;
        var entregas = await _service.GetAllAsync(dia, null, motoristaId);
        var lista    = entregas.ToList();

        // Dados da empresa (do header HTTP ou fallback)
        var empresa  = Request.Headers.TryGetValue("X-Empresa-Nome", out var emp)
            ? emp.ToString() : "TTSoft ERP";

        var html = GerarRomaneioHtml(empresa, dia, lista);
        return Content(html, "text/html; charset=utf-8");
    }

    private static string GerarRomaneioHtml(string empresa, DateTime dia,
        List<ERP.Application.DTOs.EntregaDto> entregas)
    {
        var linhas = new System.Text.StringBuilder();
        int seq    = 1;

        foreach (var e in entregas.OrderBy(x => x.Status).ThenBy(x => x.DataPrevista))
        {
            var statusBadge = e.Status switch
            {
                ERP.Domain.Enums.StatusEntrega.Entregue   => "<span style='color:#065F46;background:#D1FAE5;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:700;'>ENTREGUE</span>",
                ERP.Domain.Enums.StatusEntrega.EmRota     => "<span style='color:#92400E;background:#FEF3C7;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:700;'>EM ROTA</span>",
                ERP.Domain.Enums.StatusEntrega.Cancelada  => "<span style='color:#991B1B;background:#FEE2E2;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:700;'>CANCELADA</span>",
                ERP.Domain.Enums.StatusEntrega.Reagendada => "<span style='color:#1E3A8A;background:#DBEAFE;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:700;'>REAGENDADA</span>",
                _                                         => "<span style='color:#374151;background:#F3F4F6;padding:2px 8px;border-radius:20px;font-size:10px;font-weight:700;'>PENDENTE</span>"
            };

            linhas.Append($"""
            <tr>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;font-weight:700;text-align:center;'>{seq++}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;font-weight:700;'>{System.Net.WebUtility.HtmlEncode(e.ClienteNome)}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;font-size:11px;color:#64748B;'>{System.Net.WebUtility.HtmlEncode(e.Logradouro ?? "")} {e.Numero}, {System.Net.WebUtility.HtmlEncode(e.Bairro ?? "")} — {System.Net.WebUtility.HtmlEncode(e.Cidade ?? "")}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;font-size:11px;'>{System.Net.WebUtility.HtmlEncode(e.Referencia ?? "—")}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;font-size:11px;text-align:center;'>{e.JanelaHorario ?? "—"}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;text-align:center;'>{statusBadge}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;font-size:11px;'>{System.Net.WebUtility.HtmlEncode(e.MotoristaNome ?? "—")}</td>
              <td style='padding:10px 8px;border-bottom:1px solid #E2E8F0;text-align:center;'>
                <div style='width:140px;border-bottom:1px solid #94A3B8;margin-top:20px;'></div>
              </td>
            </tr>
            """);
        }

        var totalEntregues = entregas.Count(x => x.Status == ERP.Domain.Enums.StatusEntrega.Entregue);
        var totalPendentes = entregas.Count(x => x.Status != ERP.Domain.Enums.StatusEntrega.Entregue
                                               && x.Status != ERP.Domain.Enums.StatusEntrega.Cancelada);
        var custoTotal     = entregas.Sum(x => x.CustoEntrega);

        return $$"""
<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="UTF-8"/>
  <title>Romaneio de Entregas — {{dia:dd/MM/yyyy}}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: 'Segoe UI', sans-serif; font-size: 12px; color: #1E293B; padding: 24px; }
    @media print {
      body { padding: 0; }
      .no-print { display: none; }
      table { page-break-inside: auto; }
      tr { page-break-inside: avoid; }
    }
  </style>
</head>
<body>

  <!-- Botão de impressão -->
  <div class="no-print" style="margin-bottom:16px;">
    <button onclick="window.print()"
            style="background:#1E3A5F;color:white;border:none;padding:10px 24px;border-radius:8px;
                   font-size:13px;font-weight:700;cursor:pointer;">
      🖨️ Imprimir Romaneio
    </button>
  </div>

  <!-- Cabeçalho -->
  <div style="display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:20px;border-bottom:3px solid #1E3A5F;padding-bottom:14px;">
    <div>
      <h1 style="font-size:22px;font-weight:800;color:#1E3A5F;margin-bottom:4px;">{{System.Net.WebUtility.HtmlEncode(empresa)}}</h1>
      <h2 style="font-size:15px;font-weight:600;color:#64748B;">🚚 Romaneio de Entregas — {{dia:dddd, dd/MM/yyyy}}</h2>
    </div>
    <div style="text-align:right;">
      <div style="font-size:10px;color:#94A3B8;font-weight:600;">EMITIDO EM</div>
      <div style="font-size:13px;font-weight:700;">{{DateTime.Now:dd/MM/yyyy HH:mm}}</div>
    </div>
  </div>

  <!-- KPIs -->
  <div style="display:flex;gap:16px;margin-bottom:20px;">
    <div style="background:#F8FAFC;border-radius:8px;padding:10px 18px;border-left:4px solid #1E3A5F;">
      <div style="font-size:10px;font-weight:700;color:#64748B;">TOTAL</div>
      <div style="font-size:20px;font-weight:800;">{{entregas.Count}}</div>
    </div>
    <div style="background:#F8FAFC;border-radius:8px;padding:10px 18px;border-left:4px solid #10B981;">
      <div style="font-size:10px;font-weight:700;color:#64748B;">ENTREGUES</div>
      <div style="font-size:20px;font-weight:800;color:#065F46;">{{totalEntregues}}</div>
    </div>
    <div style="background:#F8FAFC;border-radius:8px;padding:10px 18px;border-left:4px solid #F59E0B;">
      <div style="font-size:10px;font-weight:700;color:#64748B;">PENDENTES</div>
      <div style="font-size:20px;font-weight:800;color:#92400E;">{{totalPendentes}}</div>
    </div>
    <div style="background:#F8FAFC;border-radius:8px;padding:10px 18px;border-left:4px solid #8B5CF6;">
      <div style="font-size:10px;font-weight:700;color:#64748B;">CUSTO TOTAL</div>
      <div style="font-size:20px;font-weight:800;color:#1E293B;">{{custoTotal.ToString("C")}}</div>
    </div>
  </div>

  <!-- Tabela -->
  <table style="width:100%;border-collapse:collapse;font-size:11px;">
    <thead>
      <tr style="background:#1E3A5F;color:white;">
        <th style="padding:10px 8px;text-align:center;width:36px;">#</th>
        <th style="padding:10px 8px;text-align:left;min-width:120px;">CLIENTE</th>
        <th style="padding:10px 8px;text-align:left;">ENDEREÇO</th>
        <th style="padding:10px 8px;text-align:left;width:120px;">REFERÊNCIA</th>
        <th style="padding:10px 8px;text-align:center;width:80px;">HORÁRIO</th>
        <th style="padding:10px 8px;text-align:center;width:90px;">STATUS</th>
        <th style="padding:10px 8px;text-align:left;width:100px;">MOTORISTA</th>
        <th style="padding:10px 8px;text-align:center;width:140px;">ASSINATURA</th>
      </tr>
    </thead>
    <tbody>
      {{linhas}}
    </tbody>
  </table>

  <!-- Rodapé -->
  <div style="margin-top:24px;display:flex;justify-content:space-between;align-items:flex-end;">
    <div style="font-size:10px;color:#94A3B8;">
      Gerado automaticamente pelo TTSoft ERP
    </div>
    <div style="text-align:center;">
      <div style="width:220px;border-bottom:1px solid #1E3A5F;margin-bottom:6px;"></div>
      <div style="font-size:10px;font-weight:600;">Responsável pelo despacho</div>
    </div>
  </div>

</body>
</html>
""";
    }

    // ── Veículos ──────────────────────────────────────────────────────────────

    [HttpGet("veiculos")]
    public async Task<IActionResult> GetVeiculos()
        => Ok(await _service.GetVeiculosAsync());

    [HttpPost("veiculos")]
    public async Task<IActionResult> CreateVeiculo([FromBody] CreateVeiculoDto dto)
    {
        var v = await _service.CreateVeiculoAsync(dto);
        return Ok(v);
    }

    [HttpDelete("veiculos/{id:guid}")]
    public async Task<IActionResult> DeleteVeiculo(Guid id)
    {
        await _service.DeleteVeiculoAsync(id);
        return NoContent();
    }
}
