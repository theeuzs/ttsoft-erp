// ── ERP.Api/Controllers/MetricsController.cs ─────────────────────────────────
// S2.7 — Dashboard de métricas: requests/seg, p99 latency, erros/min.
//
// Endpoint: GET /api/metrics (requer Authorize)
// Alimentado pelo MetricsMiddleware que coleta dados em memória por janela de 5 min.
//
// Para ambientes com App Insights, esses dados ficam disponíveis no Azure Portal.
// Este endpoint serve como fallback para visualização rápida sem dependência externa.
// ─────────────────────────────────────────────────────────────────────────────
using Microsoft.AspNetCore.Authorization;
using ERP.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly MetricsCollector _collector;

    public MetricsController(MetricsCollector collector) => _collector = collector;

    /// <summary>Retorna métricas de operação da API (última janela de 5 minutos).</summary>
    [HttpGet]
    public IActionResult Get() => Ok(_collector.GetSnapshot());
}
