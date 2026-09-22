// ERP.WPF/Services/HttpProductService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase C, módulo 2 (Produto) — implementação HTTP de IProductService.
/// Troca só na linha de DI (App.xaml.cs); os consumidores (PdvViewModel,
/// ProductViewModel, NotaAvulsaViewModel, ComprasViewModel,
/// HistoricoComprasViewModel, HistoricoVendasViewModel,
/// NotificacoesViewModel, SyncEngineService) não mudam.
///
/// FORA desta migração, de propósito — continuam acessando IUnitOfWork
/// direto no banco, sem passar pela API:
///   - AjusteEstoqueViewModel: bypassa IProductService inteiramente, usa
///     IUnitOfWork.Products direto. Não é consumidor de IProductService,
///     então trocar a DI aqui não afeta essa tela — ela precisa de uma
///     migração própria, decisão separada (a API já tem o endpoint certo
///     pronto: PATCH /api/products/{id}/stock).
///   - BaixarEstoqueAtomicoAsync: usado só por SaleService/DevolucaoService,
///     ambos server-side (Vendas já migrado na Fase B; Devolução ainda não
///     migrado). Não é chamado do WPF diretamente em nenhum lugar — nada a
///     fazer aqui.
///
/// Paridade com ERP.Application/Services/ProductService.cs:
///   GetAllAsync    → GET  /api/products/todos
///   SearchAsync    → GET  /api/products/busca?term=   (NÃO usa ?search=,
///                    que é GetPagedAsync — Contains simples, sem paginação
///                    por relevância; ver comentário no controller)
///   GetByIdAsync   → GET  /api/products/{id}          404 → null
///   GetByBarcodeAsync → GET /api/products/barcode/{x} 404 → null
///   GetBySkuAsync  → GET  /api/products/sku/{x}       404 → null
///   GetPagedAsync  → GET  /api/products?page=&pageSize=&search=
///   CreateAsync    → POST /api/products               400 → ValidationException
///   UpdateAsync    → PUT  /api/products/{id}          404 → KeyNotFoundException
///   DeleteAsync    → DELETE /api/products/{id}        404 → KeyNotFoundException
///   GetLowStockListAsync → GET /api/products/low-stock
///   GetLowStockCountAsync → calculado localmente a partir da lista acima —
///     não existe endpoint dedicado de contagem (nada no WPF chama a versão
///     de contagem hoje, só a lista; ver NotificacoesViewModel), então não
///     criei um endpoint só pra isso. Se algo vier a precisar só da
///     contagem, é uma chamada a mais em vez de zero — troca aceitável.
///
/// Create/Update exigem products.edit no JWT ([HasPermission] na API).
/// Antes o WPF ia direto no banco e ignorava isso.
/// </summary>
public class HttpProductService : IProductService
{
    private const string Base = "/api/products";
    private readonly HttpMessageHandler? _handler;

    public HttpProductService(HttpMessageHandler? handler = null) => _handler = handler;

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
    {
        // Timeout maior: base inteira do tenant, usada pelo SyncEngine.
        using var http = ApiHttp.CriarHttpClient(_handler, timeoutSegundos: 60);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/todos"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ProductDto>>(ApiHttp.JsonOpcoes)
               ?? new List<ProductDto>();
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/{id}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProductDto>(ApiHttp.JsonOpcoes);
    }

    public async Task<IEnumerable<ProductDto>> SearchAsync(string term)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/busca?term={Uri.EscapeDataString(term ?? string.Empty)}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ProductDto>>(ApiHttp.JsonOpcoes)
               ?? new List<ProductDto>();
    }

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/barcode/{Uri.EscapeDataString(barcode)}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProductDto>(ApiHttp.JsonOpcoes);
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/sku/{Uri.EscapeDataString(sku)}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProductDto>(ApiHttp.JsonOpcoes);
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var qs = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            qs += $"&search={Uri.EscapeDataString(search)}";

        var resp = await http.GetAsync(ApiHttp.Url($"{Base}?{qs}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PagedResult<ProductDto>>(ApiHttp.JsonOpcoes)
               ?? new PagedResult<ProductDto> { Page = page, PageSize = pageSize };
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url(Base), dto, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "cadastrar produtos");

        if (resp.StatusCode == HttpStatusCode.BadRequest)
            await LancarValidacaoAsync(resp);

        resp.EnsureSuccessStatusCode();
        return await ApiHttp.LerCorpoObrigatorioAsync<ProductDto>(resp, "criação de produto");
    }

    public async Task<ProductDto> UpdateAsync(UpdateProductDto dto)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PutAsJsonAsync(ApiHttp.Url($"{Base}/{dto.Id}"), dto, ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "editar produtos");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Produto {dto.Id} não encontrado.");
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            await LancarValidacaoAsync(resp);

        resp.EnsureSuccessStatusCode();
        return await ApiHttp.LerCorpoObrigatorioAsync<ProductDto>(resp, "atualização de produto");
    }

    public async Task DeleteAsync(Guid id)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.DeleteAsync(ApiHttp.Url($"{Base}/{id}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "excluir produtos");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Produto {id} não encontrado.");

        resp.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<ProductDto>> GetLowStockListAsync()
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/low-stock"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<ProductDto>>(ApiHttp.JsonOpcoes)
               ?? new List<ProductDto>();
    }

    /// <summary>Sem endpoint dedicado — ver comentário no topo do arquivo.</summary>
    public async Task<int> GetLowStockCountAsync()
        => (await GetLowStockListAsync()).Count();

    private static async Task LancarValidacaoAsync(HttpResponseMessage resp)
    {
        var msgs = await ApiHttp.LerMensagensDeErroAsync(resp);
        throw new ValidationException(
            string.Join(Environment.NewLine, msgs),
            msgs.Select(m => new ValidationFailure(string.Empty, m)));
    }
}
