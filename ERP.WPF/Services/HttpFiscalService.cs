// ERP.WPF/Services/HttpFiscalService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Módulo 5 (Fiscal), Etapa 1A — implementação HTTP de IFiscalService.
///
/// EmitirNotaAsync: usa o endpoint que já existe (NotasFiscaisController,
/// criado pro Portal — "S16 FIX") em vez de rodar a emissão inteira local
/// (FiscalService/Infrastructure, ainda no código mas órfão a partir daqui —
/// remoção fica pra Etapa 1C, depois de comprovado em teste). Comparação
/// campo a campo feita antes desta implementação: FinalizarVendaViewModel e
/// SaleViewModel só usam Sucesso/EmContingencia/Mensagem/UrlDanfe do
/// resultado — os 4 já vêm no corpo do endpoint, inclusive no caminho de
/// erro (BadRequest{erro} cobre o que o `else` final da UI precisa).
/// EmitirNotaAsync não recebe mais nada além de vendaId+tipoDocumento — por
/// design (ver comentário na própria interface), então local ou via HTTP é
/// literalmente a mesma entrada pro mesmo código (FiscalService reconstrói
/// tudo da Venda persistida, nunca de estado do WPF).
///
/// EmitirNotaDevolucaoAsync: Etapa 1B — mesmo padrão, endpoint próprio
/// (/{vendaId}/emitir-devolucao), ver o método abaixo.
/// </summary>
public class HttpFiscalService : IFiscalService
{
    private const string Base = "/api/notas-fiscais";
    private readonly HttpMessageHandler? _handler;

    public HttpFiscalService(HttpMessageHandler? handler = null) => _handler = handler;

    public async Task<FiscalEmissionResult> EmitirNotaAsync(Guid vendaId, string tipoDocumento)
    {
        var tipo = tipoDocumento == "NFE" ? "nfe" : "nfce";

        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsync(ApiHttp.Url($"{Base}/{tipo}/emitir-da-venda/{vendaId}"), content: null);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "emitir nota fiscal");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException(await ApiHttp.LerMensagemDeErroAsync(resp));

        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            // Mesmo formato de falha que o FiscalService local sempre devolveu
            // (Sucesso=false + Mensagem) — o controller só manda {erro} nesse
            // caminho, e é só isso que a UI (FinalizarVendaViewModel/
            // SaleViewModel) realmente olha quando Sucesso é false.
            var erro = await ApiHttp.LerMensagemDeErroAsync(resp);
            return new FiscalEmissionResult { Sucesso = false, Mensagem = erro, Status = "Falha" };
        }

        resp.EnsureSuccessStatusCode();
        var corpo = await ApiHttp.LerCorpoObrigatorioAsync<JsonElement>(resp, "emissão de nota fiscal");

        return new FiscalEmissionResult
        {
            Sucesso        = true,
            Mensagem       = corpo.TryGetProperty("mensagem", out var m) ? m.GetString() ?? string.Empty : string.Empty,
            Status         = corpo.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty,
            UrlDanfe       = corpo.TryGetProperty("urlDanfe", out var u) && u.ValueKind != JsonValueKind.Null ? u.GetString() : null,
            Ambiente       = corpo.TryGetProperty("ambiente", out var a) ? a.GetString() ?? string.Empty : string.Empty,
            EmContingencia = corpo.TryGetProperty("emContingencia", out var c) && c.ValueKind == JsonValueKind.True
        };
    }

    /// <summary>
    /// Etapa 1B do módulo 5 — implementado. Mesmo padrão de resposta do
    /// EmitirNotaAsync: FiscalService (local ou via API, mesma classe) nunca
    /// lança em falha de negócio (sem chave da nota original, rejeição
    /// SEFAZ) — sempre devolve Sucesso=false com a mensagem. Só lança
    /// KeyNotFoundException se a venda em si não existir. DevolucaoService
    /// (WPF) já lê só resultadoFiscal.Mensagem, sem checar Sucesso — o
    /// try/catch de lá é o que trata qualquer exceção daqui como
    /// informativo, sem desfazer a devolução operacional já commitada.
    /// </summary>
    public async Task<FiscalEmissionResult> EmitirNotaDevolucaoAsync(
        Guid vendaId,
        List<(Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)> itensDevolvidos,
        string motivo)
    {
        var corpo = new
        {
            Itens = itensDevolvidos
                .Select(i => new { i.ProductId, i.ProductName, i.Quantidade, i.ValorUnitario })
                .ToList(),
            Motivo = motivo
        };

        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/{vendaId}/emitir-devolucao"), corpo, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "emitir nota fiscal de devolução");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException(await ApiHttp.LerMensagemDeErroAsync(resp));

        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var erro = await ApiHttp.LerMensagemDeErroAsync(resp);
            return new FiscalEmissionResult { Sucesso = false, Mensagem = erro, Status = "Falha" };
        }

        resp.EnsureSuccessStatusCode();
        var corpoResp = await ApiHttp.LerCorpoObrigatorioAsync<JsonElement>(resp, "emissão de nota fiscal de devolução");

        return new FiscalEmissionResult
        {
            Sucesso        = true,
            Mensagem       = corpoResp.TryGetProperty("mensagem", out var m) ? m.GetString() ?? string.Empty : string.Empty,
            Status         = corpoResp.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty,
            UrlDanfe       = corpoResp.TryGetProperty("urlDanfe", out var u) && u.ValueKind != JsonValueKind.Null ? u.GetString() : null,
            Ambiente       = corpoResp.TryGetProperty("ambiente", out var a) ? a.GetString() ?? string.Empty : string.Empty,
            EmContingencia = corpoResp.TryGetProperty("emContingencia", out var c) && c.ValueKind == JsonValueKind.True
        };
    }
}