// ── ERP.Api/Services/MetricsCollector.cs ─────────────────────────────────────
// S2.7 — Coleta de métricas em memória: requests/seg, p99 latency, erros/min.
// Singleton — vive durante toda a vida da aplicação.
// Janela deslizante de 5 minutos — dados mais recentes sempre disponíveis.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Concurrent;

namespace ERP.Api.Services;

/// <summary>
/// Coleta métricas de requests em memória com janela deslizante de 5 minutos.
/// Thread-safe via ConcurrentQueue e operações atômicas.
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentQueue<RequestRecord> _records = new();
    private const int WindowMinutes = 5;

    public void Record(string path, int statusCode, double elapsedMs)
    {
        _records.Enqueue(new RequestRecord(path, statusCode, elapsedMs, DateTime.UtcNow));

        // Limpeza periódica: remove registros fora da janela (mais de 5 min)
        var cutoff = DateTime.UtcNow.AddMinutes(-WindowMinutes);
        while (_records.TryPeek(out var oldest) && oldest.At < cutoff)
            _records.TryDequeue(out _);
    }

    public MetricsSnapshot GetSnapshot()
    {
        var cutoff  = DateTime.UtcNow.AddMinutes(-WindowMinutes);
        var window  = _records.Where(r => r.At >= cutoff).ToList();
        var cutoff1 = DateTime.UtcNow.AddMinutes(-1);
        var lastMin = window.Where(r => r.At >= cutoff1).ToList();

        if (window.Count == 0)
            return new MetricsSnapshot();

        var latencies = window.Select(r => r.ElapsedMs).OrderBy(x => x).ToList();
        var p99Index  = (int)Math.Ceiling(latencies.Count * 0.99) - 1;
        var p95Index  = (int)Math.Ceiling(latencies.Count * 0.95) - 1;

        return new MetricsSnapshot
        {
            JanelaMinutos    = WindowMinutes,
            TotalRequests    = window.Count,
            RequestsPorSeg   = Math.Round(window.Count / (WindowMinutes * 60.0), 2),
            ErrosPorMin      = lastMin.Count(r => r.StatusCode >= 500),
            Erros4xxPorMin   = lastMin.Count(r => r.StatusCode is >= 400 and < 500),
            LatenciaMediaMs  = Math.Round(latencies.Average(), 1),
            LatenciaP95Ms    = Math.Round(latencies[Math.Max(0, p95Index)], 1),
            LatenciaP99Ms    = Math.Round(latencies[Math.Max(0, p99Index)], 1),
            LatenciaMaxMs    = Math.Round(latencies.Max(), 1),
            TaxaErro5xx      = Math.Round(window.Count(r => r.StatusCode >= 500) * 100.0 / window.Count, 2),
            EndpointsMaisLentos = window
                .GroupBy(r => r.Path)
                .Select(g => new EndpointMetric
                {
                    Path          = g.Key,
                    Requests      = g.Count(),
                    LatenciaMediaMs = Math.Round(g.Average(r => r.ElapsedMs), 1),
                    Erros         = g.Count(r => r.StatusCode >= 400)
                })
                .OrderByDescending(e => e.LatenciaMediaMs)
                .Take(5)
                .ToList(),
            Timestamp = DateTime.UtcNow
        };
    }

    private record RequestRecord(string Path, int StatusCode, double ElapsedMs, DateTime At);
}

public class MetricsSnapshot
{
    public int    JanelaMinutos       { get; init; }
    public int    TotalRequests       { get; init; }
    public double RequestsPorSeg      { get; init; }
    public int    ErrosPorMin         { get; init; }
    public int    Erros4xxPorMin      { get; init; }
    public double LatenciaMediaMs     { get; init; }
    public double LatenciaP95Ms       { get; init; }
    public double LatenciaP99Ms       { get; init; }
    public double LatenciaMaxMs       { get; init; }
    public double TaxaErro5xx         { get; init; }
    public List<EndpointMetric> EndpointsMaisLentos { get; init; } = new();
    public DateTime Timestamp         { get; init; }
}

public class EndpointMetric
{
    public string Path            { get; init; } = "";
    public int    Requests        { get; init; }
    public double LatenciaMediaMs { get; init; }
    public int    Erros           { get; init; }
}
