// ERP.WPF/Services/HttpCustomerService.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

namespace ERP.WPF.Services;

/// <summary>
/// Fase C, módulo 1 (Cliente) — implementação HTTP de ICustomerService.
/// Troca só na linha de DI (App.xaml.cs); os 12 consumidores (PdvViewModel,
/// CustomerViewModel, FinalizarVenda, SyncEngine, NotaAvulsa, Nfse, Orçamentos,
/// Devolução, Financeiro, SaleViewModel, SalvarOrcamentoView, QuickCustomer)
/// não mudam.
///
/// Paridade com ERP.Application/Services/CustomerService.cs, método a método:
///   GetAllAsync    → GET  /api/customers/todos          (endpoint novo, Fase C)
///   SearchAsync    → GET  /api/customers/busca?term=    (endpoint novo, Fase C)
///                    NÃO usa GET /api/customers?search= — aquele é GetPagedAsync,
///                    semântica diferente (Document.Contains + ordenado por nome;
///                    SearchAsync é Document.StartsWith + Take(50)). Trocar um
///                    pelo outro mudaria resultado da busca no PDV silenciosamente.
///   GetByIdAsync   → GET  /api/customers/{id}   404 → null (igual local)
///   GetPagedAsync  → GET  /api/customers?page=&amp;pageSize=&amp;search=
///   CreateAsync    → POST /api/customers        400 → ValidationException (igual local)
///   UpdateAsync    → PUT  /api/customers/{id}   404 → KeyNotFoundException (igual local)
///   DeleteAsync    → DELETE /api/customers/{id} 404 → KeyNotFoundException (igual local)
///
/// Mudança de comportamento REAL (não bug): Create/Update exigem
/// customers.edit e Delete exige customers.delete no JWT. Antes o WPF ia
/// direto no banco e ignorava isso. Supervisor e Vendedor NÃO têm
/// customers.delete no seed — vão receber "sem permissão" ao excluir.
/// </summary>
public class HttpCustomerService : ICustomerService
{
    private const string Base = "/api/customers";
    private readonly HttpMessageHandler? _handler;

    // Mesmo contrato do HttpSaleService: parâmetro só pra teste, DI usa o default.
    public HttpCustomerService(HttpMessageHandler? handler = null) => _handler = handler;

    public async Task<IEnumerable<CustomerDto>> GetAllAsync()
    {
        // Timeout maior: devolve a base inteira do tenant (SyncEngine e
        // FinalizarVenda usam). 30s pode ser pouco em 4G ruim da loja.
        using var http = ApiHttp.CriarHttpClient(_handler, timeoutSegundos: 60);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/todos"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CustomerDto>>(ApiHttp.JsonOpcoes)
               ?? new List<CustomerDto>();
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/{id}"));
        ApiHttp.LancarSeSessaoExpirada(resp);

        // Local: repo devolve null → service devolve null. Controller: 404.
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CustomerDto>(ApiHttp.JsonOpcoes);
    }

    public async Task<IEnumerable<CustomerDto>> SearchAsync(string term)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.GetAsync(ApiHttp.Url($"{Base}/busca?term={Uri.EscapeDataString(term ?? string.Empty)}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<CustomerDto>>(ApiHttp.JsonOpcoes)
               ?? new List<CustomerDto>();
    }

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var qs = $"page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            qs += $"&search={Uri.EscapeDataString(search)}";

        var resp = await http.GetAsync(ApiHttp.Url($"{Base}?{qs}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();

        // PagedResult.TotalPages é calculado (TotalItems/PageSize) — não
        // precisa vir no JSON, recalcula sozinho após desserializar.
        return await resp.Content.ReadFromJsonAsync<PagedResult<CustomerDto>>(ApiHttp.JsonOpcoes)
               ?? new PagedResult<CustomerDto> { Page = page, PageSize = pageSize };
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PostAsJsonAsync(ApiHttp.Url(Base), ParaEnvio(dto), ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "cadastrar clientes");

        if (resp.StatusCode == HttpStatusCode.BadRequest)
            await LancarValidacaoAsync(resp);

        resp.EnsureSuccessStatusCode();
        return await ApiHttp.LerCorpoObrigatorioAsync<CustomerDto>(resp, "criação de cliente");
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, CreateCustomerDto dto)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.PutAsJsonAsync(ApiHttp.Url($"{Base}/{id}"), ParaEnvio(dto), ApiHttp.JsonOpcoes);
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "editar clientes");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Cliente {id} não encontrado.");
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            await LancarValidacaoAsync(resp);

        resp.EnsureSuccessStatusCode();
        return await ApiHttp.LerCorpoObrigatorioAsync<CustomerDto>(resp, "atualização de cliente");
    }

    public async Task DeleteAsync(Guid id)
    {
        using var http = ApiHttp.CriarHttpClient(_handler);
        var resp = await http.DeleteAsync(ApiHttp.Url($"{Base}/{id}"));
        ApiHttp.LancarSeSessaoExpirada(resp);
        ApiHttp.LancarSeAcessoNegado(resp, "excluir clientes");

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Cliente {id} não encontrado.");

        resp.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// S{N} FIX (Fase C): CreateCustomerDto.Document é `string` não-anulável,
    /// mas CustomerViewModel/QuickCustomer mandam null quando o CPF está vazio.
    /// Local isso passava (objeto em memória). Via HTTP, o [ApiController] com
    /// Nullable habilitado trata string não-anulável como [Required] implícito
    /// e devolve 400 "The Document field is required." ANTES de chegar no
    /// service — cliente sem CPF simplesmente pararia de salvar.
    /// Normaliza pra "" numa CÓPIA (não muta o objeto do chamador); o
    /// CustomerService já converte vazio → null no banco (Create sempre fez
    /// isso; Update passou a fazer na mesma entrega — ver CustomerService).
    /// </summary>
    private static CreateCustomerDto ParaEnvio(CreateCustomerDto dto) => new()
    {
        Document          = dto.Document ?? string.Empty,
        Name              = dto.Name ?? string.Empty,
        StateRegistration = dto.StateRegistration,
        Phone             = dto.Phone,
        Email             = dto.Email,
        ZipCode           = dto.ZipCode,
        Street            = dto.Street,
        Number            = dto.Number,
        Complement        = dto.Complement,
        Neighborhood      = dto.Neighborhood,
        City              = dto.City,
        State             = dto.State,
        GrupoPreco        = dto.GrupoPreco,
        LimiteCredito     = dto.LimiteCredito
    };

    /// <summary>Local, CreateAsync lança FluentValidation.ValidationException
    /// (ValidateAndThrowAsync). Preservado o TIPO pra qualquer catch específico
    /// futuro; ValidationException herda de Exception, então os catch(Exception)
    /// atuais mostram ex.Message normalmente.</summary>
    private static async Task LancarValidacaoAsync(HttpResponseMessage resp)
    {
        var msgs = await ApiHttp.LerMensagensDeErroAsync(resp);
        throw new ValidationException(
            string.Join(Environment.NewLine, msgs),
            msgs.Select(m => new ValidationFailure(string.Empty, m)));
    }
}
