// ERP.Tests/WPF/HttpProductServiceTests.cs
using ERP.Application.DTOs;
using ERP.WPF.Services;
using ERP.WPF.State;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// Fase C, módulo Produto — mesmo estilo do HttpCustomerServiceTests: handler
/// falso mínimo, sem biblioteca de mock HTTP. Foco nos pontos onde a troca
/// local→HTTP podia mudar comportamento sem ninguém perceber: rota da busca
/// (bipe por código de barras primeiro, depois busca por palavra), 404 em
/// GetByBarcode/GetBySku, mapeamento de 400/403/404.
/// </summary>
public class HttpProductServiceTests
{
    public HttpProductServiceTests() => AppSession.ApiBaseUrl = "http://localhost";

    private sealed class HandlerGravador : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string? _corpoJson;
        public HttpRequestMessage? UltimaRequisicao { get; private set; }
        public string? UltimoCorpo { get; private set; }

        public HandlerGravador(HttpStatusCode status, string? corpoJson = null)
            => (_status, _corpoJson) = (status, corpoJson);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaRequisicao = request;
            UltimoCorpo = request.Content != null ? await request.Content.ReadAsStringAsync(ct) : null;
            var resp = new HttpResponseMessage(_status);
            if (_corpoJson != null)
                resp.Content = new StringContent(_corpoJson, Encoding.UTF8, "application/json");
            return resp;
        }
    }

    private const string ProdutoJson =
        "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"name\":\"PARAFUSO PHILLIPS 4.0X40\"," +
        "\"barcode\":\"7891234588888\",\"sku\":\"1653800\",\"categoryName\":null,\"brand\":null," +
        "\"unit\":\"UN\",\"salePrice\":0.40,\"stock\":842,\"minStock\":50,\"isActive\":true," +
        "\"emCampanha\":false,\"imageUrl\":null,\"descricaoDetalhada\":null}";

    [Fact(DisplayName = "SearchAsync usa /busca, NÃO ?search= (GetPaged, semântica diferente)")]
    public async Task SearchAsync_UsaEndpointDeBusca()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "[]");
        await new HttpProductService(h).SearchAsync("parafuso 4.0x40");

        var uri = h.UltimaRequisicao!.RequestUri!;
        uri.AbsolutePath.Should().Be("/api/products/busca");
        Uri.UnescapeDataString(uri.Query).Should().Be("?term=parafuso 4.0x40");
    }

    [Fact(DisplayName = "GetAllAsync usa /todos")]
    public async Task GetAllAsync_UsaEndpointTodos()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, $"[{ProdutoJson}]");
        var lista = (await new HttpProductService(h).GetAllAsync()).ToList();

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/products/todos");
        lista.Should().ContainSingle();
    }

    [Fact(DisplayName = "GetByBarcodeAsync — acha produto (fluxo do bipe do PDV)")]
    public async Task GetByBarcodeAsync_Encontrado_DesserializaProduto()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, ProdutoJson);
        var produto = await new HttpProductService(h).GetByBarcodeAsync("7891234588888");

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/products/barcode/7891234588888");
        produto!.Name.Should().Be("PARAFUSO PHILLIPS 4.0X40");
        produto.Stock.Should().Be(842);
    }

    [Fact(DisplayName = "GetByBarcodeAsync — 404 vira null, não lança (PDV precisa cair pra busca por nome)")]
    public async Task GetByBarcodeAsync_404_RetornaNull()
        => (await new HttpProductService(new HandlerGravador(HttpStatusCode.NotFound)).GetByBarcodeAsync("XYZ"))
            .Should().BeNull();

    [Fact(DisplayName = "GetByBarcodeAsync — string vazia não faz chamada HTTP nenhuma")]
    public async Task GetByBarcodeAsync_Vazio_NaoChamaApi()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, ProdutoJson);
        var produto = await new HttpProductService(h).GetByBarcodeAsync("");

        produto.Should().BeNull();
        h.UltimaRequisicao.Should().BeNull("string vazia é tratada localmente, sem ida à API");
    }

    [Fact(DisplayName = "GetBySkuAsync — 404 vira null")]
    public async Task GetBySkuAsync_404_RetornaNull()
        => (await new HttpProductService(new HandlerGravador(HttpStatusCode.NotFound)).GetBySkuAsync("SKU-X"))
            .Should().BeNull();

    [Fact(DisplayName = "GetByIdAsync — 401 vira SessaoExpiradaException antes do tratamento de 404")]
    public async Task GetByIdAsync_401_LancaSessaoExpirada()
    {
        var act = async () => await new HttpProductService(new HandlerGravador(HttpStatusCode.Unauthorized))
            .GetByIdAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<ERP.Application.Exceptions.SessaoExpiradaException>();
    }

    [Fact(DisplayName = "CreateAsync — 400 {erros:[...]} vira FluentValidation.ValidationException")]
    public async Task CreateAsync_400Erros_LancaValidationException()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest, "{\"erros\":[\"Nome é obrigatório.\"]}");
        var act = async () => await new HttpProductService(h).CreateAsync(new CreateProductDto());

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>())
            .WithMessage("*Nome é obrigatório.*");
    }

    [Fact(DisplayName = "CreateAsync — 403 vira UnauthorizedAccessException legível")]
    public async Task CreateAsync_403_LancaUnauthorizedAccess()
    {
        var act = async () => await new HttpProductService(new HandlerGravador(HttpStatusCode.Forbidden))
            .CreateAsync(new CreateProductDto { Name = "X", Unit = "UN" });

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*permissão*cadastrar produtos*");
    }

    [Fact(DisplayName = "UpdateAsync — PUT vai pra /api/products/{id} (rota usa o Id do próprio DTO)")]
    public async Task UpdateAsync_UsaIdDoDtoNaRota()
    {
        var id = Guid.NewGuid();
        var h  = new HandlerGravador(HttpStatusCode.OK, ProdutoJson);
        await new HttpProductService(h).UpdateAsync(new UpdateProductDto { Id = id, Name = "X", Unit = "UN" });

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/products/{id}");
        h.UltimaRequisicao.Method.Should().Be(HttpMethod.Put);
    }

    [Fact(DisplayName = "UpdateAsync / DeleteAsync — 404 vira KeyNotFoundException")]
    public async Task UpdateEDelete_404_LancaKeyNotFound()
    {
        var svc = new HttpProductService(new HandlerGravador(HttpStatusCode.NotFound));

        await ((Func<Task>)(() => svc.UpdateAsync(new UpdateProductDto { Id = Guid.NewGuid(), Name = "X", Unit = "UN" })))
            .Should().ThrowAsync<KeyNotFoundException>();
        await ((Func<Task>)(() => svc.DeleteAsync(Guid.NewGuid())))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "GetLowStockListAsync usa /low-stock")]
    public async Task GetLowStockListAsync_UsaEndpointCerto()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, $"[{ProdutoJson}]");
        var lista = (await new HttpProductService(h).GetLowStockListAsync()).ToList();

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/products/low-stock");
        lista.Should().ContainSingle();
    }

    [Fact(DisplayName = "GetLowStockCountAsync — sem endpoint dedicado, conta a partir da lista")]
    public async Task GetLowStockCountAsync_ContaAPartirDaLista()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, $"[{ProdutoJson},{ProdutoJson}]");
        (await new HttpProductService(h).GetLowStockCountAsync()).Should().Be(2);
    }

    [Fact(DisplayName = "SearchAsync — 503 sobe como HttpRequestException (classificador de conectividade decide)")]
    public async Task SearchAsync_503_PropagaHttpRequestException()
    {
        var act = async () => await new HttpProductService(new HandlerGravador(HttpStatusCode.ServiceUnavailable))
            .SearchAsync("x");
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
