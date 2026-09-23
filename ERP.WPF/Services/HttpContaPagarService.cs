// ERP.WPF/Services/HttpContaPagarService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase C, módulo 4 (Contas a Pagar) — implementação HTTP de
/// IContaPagarService. Único consumidor real desta interface no WPF é
/// NotificacoesViewModel (GetVencendoHojeAsync — indicador de contas
/// vencendo hoje). A tela cheia de Contas a Pagar (ContaPagarViewModel)
/// NÃO usa esta interface — faz CRUD completo direto no banco via
/// IUnitOfWork (Update/Delete/pagamento com escolha de origem entre Caixa
/// e Conta Bancária), e a API hoje não tem esses métodos. Migrar essa DI
/// aqui não afeta aquela tela nem um pouco — ela nunca pediu
/// IContaPagarService pro DI, então continua exatamente como estava.
/// Decisão explícita, documentada, mesmo padrão do AjusteEstoqueViewModel
/// na Fase C módulo Caixa: extender a API pra cobrir edição/exclusão/
/// pagamento-com-origem de Conta a Pagar é trabalho novo, não migração —
/// fica pra uma entrega própria.
/// </summary>
public class HttpContaPagarService : IContaPagarService
{
    private const string Base = "/api/contas-pagar";
    private readonly HttpMessageHandler? _handler;

    public HttpContaPagarService(HttpMessageHandler? handler = null) => _handler = handler;

    /// <summary>Sem endpoint — sem uso real hoje (grep confirma: nenhum chamador
    /// em lugar nenhum do código, WPF ou API, além da própria declaração).</summary>
    public Task<int> CountVencendoHojeAsync()
        => throw new NotSupportedException(
            "CountVencendoHojeAsync não tem endpoint — nenhum consumidor real " +
            "chama isso hoje (comentário na interface dizia 'usado pelo " +
            "DashboardService', mas DashboardService usa IUnitOfWork direto, " +
            "não esse método).");

    public async Task<IEnumerable<(string Descricao, decimal Valor)>> GetVencendoHojeAsync()
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/vencendo-hoje"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();

        var itens = await resp.Content.ReadFromJsonAsync<List<VencendoHojeItem>>(ApiHttp.JsonOpcoes)
                    ?? new List<VencendoHojeItem>();
        return itens.Select(i => (i.Descricao, i.Valor));
    }

    private sealed class VencendoHojeItem
    {
        public string  Descricao { get; set; } = string.Empty;
        public decimal Valor     { get; set; }
    }

    public async Task<IReadOnlyList<ContaPagarDto>> GetPendentesAsync(CancellationToken ct = default)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/pendentes"), ct);
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ContaPagarDto>>(ApiHttp.JsonOpcoes, ct) ?? new List<ContaPagarDto>();
    }

    public async Task<IReadOnlyList<ContaPagarDto>> GetVencidasAsync(CancellationToken ct = default)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/vencidas"), ct);
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ContaPagarDto>>(ApiHttp.JsonOpcoes, ct) ?? new List<ContaPagarDto>();
    }

    public async Task<ContaPagarResumoDto> GetResumoAsync(CancellationToken ct = default)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/resumo"), ct);
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await ApiHttp.LerCorpoObrigatorioAsync<ContaPagarResumoDto>(resp, "resumo de contas a pagar");
    }

    public async Task<ContaPagarDto> CreateAsync(CreateContaPagarDto dto, CancellationToken ct = default)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url(Base), dto, ApiHttp.JsonOpcoes, ct);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "lançar contas a pagar");
        resp.EnsureSuccessStatusCode();
        return await ApiHttp.LerCorpoObrigatorioAsync<ContaPagarDto>(resp, "criação de conta a pagar");
    }

    public async Task PagarAsync(Guid id, CancellationToken ct = default)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsync(ApiHttp.Url($"{Base}/{id}/pagar"), content: null, ct);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "pagar contas a pagar");
        resp.EnsureSuccessStatusCode();
    }

    public async Task CancelarAsync(Guid id, CancellationToken ct = default)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsync(ApiHttp.Url($"{Base}/{id}/cancelar"), content: null, ct);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "cancelar contas a pagar");
        resp.EnsureSuccessStatusCode();
    }
}
