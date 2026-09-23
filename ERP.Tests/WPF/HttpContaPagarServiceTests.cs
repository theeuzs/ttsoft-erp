// ERP.Tests/WPF/HttpContaPagarServiceTests.cs
using ERP.Application.DTOs;
using ERP.WPF.Services;
using ERP.WPF.State;
using FluentAssertions;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// Fase C, módulo 4 (Contas a Pagar) — único consumidor real é
/// NotificacoesViewModel (GetVencendoHojeAsync). ContaPagarViewModel (tela
/// cheia) bypassa esta interface inteira, decisão documentada — ver
/// HttpContaPagarService.cs.
/// </summary>
public class HttpContaPagarServiceTests
{
    public HttpContaPagarServiceTests() => AppSession.ApiBaseUrl = "http://localhost";

    private sealed class HandlerGravador : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string? _corpoJson;
        public HttpRequestMessage? UltimaRequisicao { get; private set; }

        public HandlerGravador(HttpStatusCode status, string? corpoJson = null)
            => (_status, _corpoJson) = (status, corpoJson);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UltimaRequisicao = request;
            var resp = new HttpResponseMessage(_status);
            if (_corpoJson != null)
                resp.Content = new StringContent(_corpoJson, Encoding.UTF8, "application/json");
            return Task.FromResult(resp);
        }
    }

    [Fact(DisplayName = "GetVencendoHojeAsync — reconstrói a tupla (Descricao,Valor) a partir do DTO da API")]
    public async Task GetVencendoHojeAsync_ReconstroiTupla()
    {
        var h = new HandlerGravador(HttpStatusCode.OK,
            "[{\"descricao\":\"Aluguel\",\"valor\":1200.00},{\"descricao\":\"Água\",\"valor\":85.30}]");

        var itens = (await new HttpContaPagarService(h).GetVencendoHojeAsync()).ToList();

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/contas-pagar/vencendo-hoje");
        itens.Should().HaveCount(2);
        itens[0].Descricao.Should().Be("Aluguel");
        itens[0].Valor.Should().Be(1200.00m);
    }

    [Fact(DisplayName = "CountVencendoHojeAsync — sem endpoint, sem uso real, lança NotSupportedException")]
    public async Task CountVencendoHojeAsync_LancaNotSupported()
    {
        var act = async () => await new HttpContaPagarService().CountVencendoHojeAsync();
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact(DisplayName = "CreateAsync — usa a rota certa e desserializa o DTO criado")]
    public async Task CreateAsync_UsaRotaCerta()
    {
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"id\":\"11111111-1111-1111-1111-111111111111\",\"descricao\":\"Teste\",\"valor\":50,\"categoria\":\"Geral\",\"dataVencimento\":\"2026-10-01\",\"status\":\"Pendente\"}");

        var criada = await new HttpContaPagarService(h).CreateAsync(
            new CreateContaPagarDto { Descricao = "Teste", Valor = 50m, DataVencimento = new DateTime(2026, 10, 1) });

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/contas-pagar");
        h.UltimaRequisicao.Method.Should().Be(HttpMethod.Post);
        criada.Descricao.Should().Be("Teste");
    }

    [Fact(DisplayName = "PagarAsync — POST sem corpo pra /{id}/pagar")]
    public async Task PagarAsync_PostSemCorpo()
    {
        var id = Guid.NewGuid();
        var h = new HandlerGravador(HttpStatusCode.OK, "{\"mensagem\":\"ok\"}");
        await new HttpContaPagarService(h).PagarAsync(id);

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/contas-pagar/{id}/pagar");
    }

    [Fact(DisplayName = "PagarAsync — 403 vira UnauthorizedAccessException legível")]
    public async Task PagarAsync_403_LancaUnauthorizedAccess()
    {
        var act = async () => await new HttpContaPagarService(new HandlerGravador(HttpStatusCode.Forbidden))
            .PagarAsync(Guid.NewGuid());
        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*permissão*pagar contas a pagar*");
    }

    [Fact(DisplayName = "GetResumoAsync — desserializa ContaPagarResumoDto direto (sem tupla)")]
    public async Task GetResumoAsync_DesserializaDireto()
    {
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"totalPendente\":800,\"totalVencido\":100,\"qtdContas\":5,\"qtdVencidas\":1}");

        var resumo = await new HttpContaPagarService(h).GetResumoAsync();

        resumo.TotalPendente.Should().Be(800m);
        resumo.QtdVencidas.Should().Be(1);
    }
}
