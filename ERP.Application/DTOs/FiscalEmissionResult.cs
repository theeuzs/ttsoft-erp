// ── ERP.Application/DTOs/FiscalEmissionResult.cs ───────────────────────────
namespace ERP.Application.DTOs;

/// <summary>
/// Resultado de uma tentativa de emissão fiscal — substitui o antigo tuple
/// solto (bool sucesso, string mensagem, string urlDanfe) espalhado pelo
/// FinalizarVendaViewModel. Quem decide o que fazer com isso (mostrar
/// MessageBox, abrir navegador) é a UI — esse objeto só carrega o fato.
/// </summary>
public class FiscalEmissionResult
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;

    /// <summary>"Autorizada", "Contingência", "Processando" — mesmo vocabulário
    /// já usado em Sale.NfceStatusFocus hoje.</summary>
    public string Status { get; set; } = string.Empty;

    public string? UrlDanfe { get; set; }
    public string Ambiente { get; set; } = string.Empty;

    /// <summary>true quando caiu no modo contingência (falha de comunicação,
    /// não erro de validação) — a UI usa isso pra decidir a mensagem certa.</summary>
    public bool EmContingencia { get; set; }
}