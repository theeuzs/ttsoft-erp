// ERP.WPF/Services/HttpContaReceberService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase C, módulo 4 (Contas a Receber) — implementação HTTP de
/// IContaReceberService. Troca só na linha de DI (App.xaml.cs); consumidores:
/// FinanceiroViewModel, CustomerHistoryViewModel, MainWindow, NotificacoesViewModel.
///
/// Achado auditando ANTES de migrar (lição do módulo Caixa aplicada aqui):
/// MotorFinanceiroService roda TANTO no servidor quanto no WPF e chama
/// ExisteContaParaSalePaymentAsync + GerarContaAPrazoAsync direto — os dois
/// não tinham endpoint. Adicionados antes desta implementação existir, não
/// depois de quebrar em produção.
///
/// GetBySaleIdAsync e GetParcelasByVendaAsync NÃO têm endpoint — confirmado
/// (grep) que nenhum consumidor real chama esses dois via a interface do
/// serviço (GetBySaleIdAsync só é usado via repositório direto dentro do
/// próprio SaleService, server-side; GetParcelasByVendaAsync não tem
/// nenhum chamador em lugar nenhum do código hoje).
/// </summary>
public class HttpContaReceberService : IContaReceberService
{
    private const string Base = "/api/contas-receber";
    private readonly HttpMessageHandler? _handler;

    public HttpContaReceberService(HttpMessageHandler? handler = null) => _handler = handler;

    public async Task GerarContaAPrazoAsync(Guid clienteId, Guid? vendaId, decimal valor, string descricao, Guid? salePaymentId = null)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/gerar-a-prazo"),
            new { ClienteId = clienteId, VendaId = vendaId, Valor = valor, Descricao = descricao, SalePaymentId = salePaymentId },
            ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<bool> ExisteContaParaSalePaymentAsync(Guid salePaymentId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/existe-para-sale-payment/{salePaymentId}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<bool>(ApiHttp.JsonOpcoes);
    }

    public Task<IEnumerable<ContaReceber>> GetBySaleIdAsync(Guid saleId)
        => throw new NotSupportedException(
            "GetBySaleIdAsync (via ICaixaReceberService) não tem endpoint — uso " +
            "real hoje é só via IUnitOfWork.ContasReceber direto, dentro do " +
            "SaleService (server-side). Nada no WPF chama isso por aqui.");

    public async Task<IEnumerable<ParcelaDto>> GerarParcelasAsync(GerarParcelasDto dto)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/parcelar"), dto, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ParcelaDto>>(ApiHttp.JsonOpcoes) ?? new List<ParcelaDto>();
    }

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByParcelamentoAsync(Guid parcelamentoId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/parcelamento/{parcelamentoId}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ParcelaDto>>(ApiHttp.JsonOpcoes) ?? new List<ParcelaDto>();
    }

    public Task<IEnumerable<ParcelaDto>> GetParcelasByVendaAsync(Guid vendaId)
        => throw new NotSupportedException(
            "GetParcelasByVendaAsync não tem endpoint — nenhum consumidor real " +
            "chama isso hoje em lugar nenhum do código (WPF ou API).");

    public async Task<IEnumerable<ContaReceber>> GetPendentesAsync()
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/pendentes"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ContaReceber>>(ApiHttp.JsonOpcoes) ?? new List<ContaReceber>();
    }

    public async Task<IEnumerable<ContaReceber>> GetPorClienteAsync(Guid clienteId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/cliente/{clienteId}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ContaReceber>>(ApiHttp.JsonOpcoes) ?? new List<ContaReceber>();
    }

    public async Task<IEnumerable<ContaReceber>> GetInadimplentesAsync()
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/inadimplentes"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ContaReceber>>(ApiHttp.JsonOpcoes) ?? new List<ContaReceber>();
    }

    public async Task DarBaixaParcialAsync(Guid contaId, decimal valorRecebido)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/{contaId}/baixa-parcial"),
            new { Valor = valorRecebido }, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException(await ApiHttp.LerMensagemDeErroAsync(resp));
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public async Task DarBaixaTotalAsync(Guid contaId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsync(ApiHttp.Url($"{Base}/{contaId}/baixa-total"), content: null);
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
    }

    public async Task CancelarAsync(Guid contaId, string motivo)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/{contaId}/cancelar"),
            new { Motivo = motivo }, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException(await ApiHttp.LerMensagemDeErroAsync(resp));
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public async Task DarDescontoAsync(Guid contaId, decimal valorDesconto, string motivo)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/{contaId}/desconto"),
            new { ValorDesconto = valorDesconto, Motivo = motivo }, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException(await ApiHttp.LerMensagemDeErroAsync(resp));
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public async Task DarBaixaEmLoteAsync(IEnumerable<Guid> contaIds, decimal valorAPagar, decimal valorDesconto, string formaPagamento)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url($"{Base}/baixa-em-lote"),
            new { ContaIds = contaIds.ToList(), ValorAPagar = valorAPagar, ValorDesconto = valorDesconto, FormaPagamento = formaPagamento },
            ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await ApiHttp.LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    private sealed class ResumoResponse
    {
        public decimal TotalPendente { get; set; }
        public decimal TotalVencido  { get; set; }
        public int     QtdClientes   { get; set; }
    }

    public async Task<(decimal TotalPendente, decimal TotalVencido, int QtdClientes)> GetResumoAsync()
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/resumo"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();

        var r = await ApiHttp.LerCorpoObrigatorioAsync<ResumoResponse>(resp, "resumo de contas a receber");
        return (r.TotalPendente, r.TotalVencido, r.QtdClientes);
    }

    public async Task<int> CountInadimplentesAsync()
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/inadimplentes/count"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<int>(ApiHttp.JsonOpcoes);
    }

    public async Task<IEnumerable<ContaReceberEvento>> GetEventosAsync(Guid contaId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/{contaId}/eventos"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ContaReceberEvento>>(ApiHttp.JsonOpcoes) ?? new List<ContaReceberEvento>();
    }

    /// <summary>Boleto via Asaas — S17 FIX (ver interface): antes o WPF nunca
    /// tinha AsaasService registrado, então isso sempre devolvia
    /// AsaasIndisponivel localmente. Migrado pra HTTP, passa a funcionar de
    /// verdade — a API sempre tem Asaas configurado. Efeito colateral bom
    /// da migração, não algo que eu precisei construir.</summary>
    public async Task<GerarBoletoResultado> GerarBoletoAsync(Guid contaId)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsync(ApiHttp.Url($"{Base}/{contaId}/gerar-boleto"), content: null);
        ApiHttp.LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return new GerarBoletoResultado(GerarBoletoStatus.ContaNaoEncontrada);

        if ((int)resp.StatusCode == 502 || (int)resp.StatusCode == 503)
        {
            var corpo = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var erro  = corpo.TryGetProperty("erro", out var e) ? e.GetString() : null;
            var status = resp.StatusCode == HttpStatusCode.ServiceUnavailable
                ? GerarBoletoStatus.AsaasIndisponivel
                : GerarBoletoStatus.FalhaAoGerarBoleto;
            return new GerarBoletoResultado(status, erro);
        }

        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var corpo = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var erro  = corpo.TryGetProperty("erro", out var e) ? e.GetString() : null;
            return new GerarBoletoResultado(GerarBoletoStatus.ClienteSemDocumento, erro);
        }

        resp.EnsureSuccessStatusCode();
        var r = await ApiHttp.LerCorpoObrigatorioAsync<System.Text.Json.JsonElement>(resp, "geração de boleto");
        string? Get(string nome) => r.TryGetProperty(nome, out var v) && v.ValueKind != System.Text.Json.JsonValueKind.Null ? v.GetString() : null;

        return new GerarBoletoResultado(
            GerarBoletoStatus.Sucesso,
            BoletoUrl:     Get("boletoUrl"),
            InvoiceUrl:    Get("invoiceUrl"),
            BoletoBarCode: Get("boletoBarCode"),
            AsaasStatus:   Get("asaasStatus"));
    }
}
