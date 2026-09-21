// ERP.Tests/WPF/HttpCustomerServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.Exceptions;
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
/// Fase C, módulo Cliente — mesmo estilo do HttpSaleServiceTests: handler falso
/// mínimo, sem biblioteca de mock HTTP. Foco nos pontos onde a troca
/// local→HTTP podia mudar comportamento sem ninguém perceber: rota da busca,
/// null no Document, mapeamento de 400/403/404.
/// </summary>
public class HttpCustomerServiceTests
{
    public HttpCustomerServiceTests() => AppSession.ApiBaseUrl = "http://localhost";

    /// <summary>Devolve resposta fixa e guarda a última requisição recebida
    /// (URL + corpo) pra asserção.</summary>
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

    private const string ClienteJson =
        "{\"id\":\"11111111-1111-1111-1111-111111111111\",\"document\":\"12345678900\",\"name\":\"Fulano\"," +
        "\"phone\":null,\"city\":\"Curitiba\",\"haverBalance\":10.5,\"email\":\"f@x.com\",\"complement\":\"Fundos\"}";

    [Fact(DisplayName = "SearchAsync usa /busca (StartsWith+Take50), NÃO ?search= (GetPaged, semântica diferente)")]
    public async Task SearchAsync_UsaEndpointDeBusca()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "[]");
        await new HttpCustomerService(h).SearchAsync("123 456");

        var uri = h.UltimaRequisicao!.RequestUri!;
        uri.AbsolutePath.Should().Be("/api/customers/busca");
        Uri.UnescapeDataString(uri.Query).Should().Be("?term=123 456");
    }

    [Fact(DisplayName = "GetAllAsync usa /todos")]
    public async Task GetAllAsync_UsaEndpointTodos()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, $"[{ClienteJson}]");
        var lista = (await new HttpCustomerService(h).GetAllAsync()).ToList();

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/customers/todos");
        lista.Should().ContainSingle();
    }

    [Fact(DisplayName = "GetByIdAsync — 404 vira null (igual CustomerService local)")]
    public async Task GetByIdAsync_404_RetornaNull()
        => (await new HttpCustomerService(new HandlerGravador(HttpStatusCode.NotFound)).GetByIdAsync(Guid.NewGuid()))
            .Should().BeNull();

    [Fact(DisplayName = "GetByIdAsync — desserializa Email/Complement/HaverBalance (campo que a tela lê precisa chegar)")]
    public async Task GetByIdAsync_DesserializaCamposQueATelaLe()
    {
        var c = await new HttpCustomerService(new HandlerGravador(HttpStatusCode.OK, ClienteJson))
            .GetByIdAsync(Guid.NewGuid());

        c!.Name.Should().Be("Fulano");
        c.Email.Should().Be("f@x.com");
        c.Complement.Should().Be("Fundos");
        c.HaverBalance.Should().Be(10.5m);
    }

    [Fact(DisplayName = "GetByIdAsync — 401 vira SessaoExpiradaException antes do tratamento de 404")]
    public async Task GetByIdAsync_401_LancaSessaoExpirada()
    {
        var act = async () => await new HttpCustomerService(new HandlerGravador(HttpStatusCode.Unauthorized))
            .GetByIdAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<SessaoExpiradaException>();
    }

    [Fact(DisplayName = "CreateAsync — Document null vai como \"\" (senão [ApiController] devolve 400 de campo obrigatório)")]
    public async Task CreateAsync_DocumentNull_EnviaStringVazia_SemMutarOriginal()
    {
        var h = new HandlerGravador(HttpStatusCode.Created, ClienteJson);
        var dto = new CreateCustomerDto { Name = "Sem CPF", Document = null! };

        await new HttpCustomerService(h).CreateAsync(dto);

        using var json = JsonDocument.Parse(h.UltimoCorpo!);
        json.RootElement.GetProperty("Document").GetString().Should().Be(string.Empty);
        dto.Document.Should().BeNull("o objeto do chamador não pode ser alterado");
    }

    [Fact(DisplayName = "CreateAsync — 400 {erros:[...]} vira FluentValidation.ValidationException com as mensagens")]
    public async Task CreateAsync_400Erros_LancaValidationException()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest, "{\"erros\":[\"Nome é obrigatório.\"]}");
        var act = async () => await new HttpCustomerService(h).CreateAsync(new CreateCustomerDto());

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>())
            .WithMessage("*Nome é obrigatório.*");
    }

    [Fact(DisplayName = "CreateAsync — 400 ProblemDetails (validação automática) também é lido")]
    public async Task CreateAsync_400ProblemDetails_LeMensagem()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest,
            "{\"title\":\"One or more validation errors occurred.\",\"errors\":{\"Document\":[\"The Document field is required.\"]}}");
        var act = async () => await new HttpCustomerService(h).CreateAsync(new CreateCustomerDto { Name = "X" });

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>())
            .WithMessage("*Document field is required*");
    }

    [Fact(DisplayName = "DeleteAsync — 403 vira UnauthorizedAccessException legível, não HttpRequestException")]
    public async Task DeleteAsync_403_LancaUnauthorizedAccess()
    {
        var act = async () => await new HttpCustomerService(new HandlerGravador(HttpStatusCode.Forbidden))
            .DeleteAsync(Guid.NewGuid());

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*permissão*excluir clientes*");
    }

    [Fact(DisplayName = "UpdateAsync / DeleteAsync — 404 vira KeyNotFoundException (igual local)")]
    public async Task UpdateEDelete_404_LancaKeyNotFound()
    {
        var svc = new HttpCustomerService(new HandlerGravador(HttpStatusCode.NotFound));

        await ((Func<Task>)(() => svc.UpdateAsync(Guid.NewGuid(), new CreateCustomerDto { Name = "X" })))
            .Should().ThrowAsync<KeyNotFoundException>();
        await ((Func<Task>)(() => svc.DeleteAsync(Guid.NewGuid())))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "SearchAsync — 503 sobe como HttpRequestException (classificador de conectividade decide)")]
    public async Task SearchAsync_503_PropagaHttpRequestException()
    {
        var act = async () => await new HttpCustomerService(new HandlerGravador(HttpStatusCode.ServiceUnavailable))
            .SearchAsync("x");
        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
