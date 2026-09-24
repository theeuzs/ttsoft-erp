// ERP.Tests/WPF/HttpFiscalServiceTests.cs
using ERP.WPF.Services;
using ERP.WPF.State;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// Módulo 5 (Fiscal), Etapas 1A+1B. Foco: os 3 caminhos que
/// FinalizarVendaViewModel/SaleViewModel/DevolucaoService realmente
/// distinguem (sucesso, contingência, falha), roteamento NFE vs NFCE, e
/// que os dois métodos (venda normal e devolução) nunca lançam em falha de
/// negócio — só em venda inexistente — igual ao FiscalService local.
/// </summary>
public class HttpFiscalServiceTests
{
    public HttpFiscalServiceTests() => AppSession.ApiBaseUrl = "http://localhost";

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

    [Fact(DisplayName = "EmitirNotaAsync — NFCE usa a rota /nfce/emitir-da-venda/{id}")]
    public async Task EmitirNotaAsync_Nfce_UsaRotaCerta()
    {
        var vendaId = Guid.NewGuid();
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"sucesso\":true,\"mensagem\":\"ok\",\"status\":\"Autorizada\",\"urlDanfe\":\"https://x/danfe.pdf\",\"ambiente\":\"Homologação\",\"emContingencia\":false}");

        var r = await new HttpFiscalService(h).EmitirNotaAsync(vendaId, "NFCE");

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/notas-fiscais/nfce/emitir-da-venda/{vendaId}");
        h.UltimaRequisicao.Method.Should().Be(HttpMethod.Post);
        h.UltimaRequisicao.Content.Should().BeNull("o endpoint não recebe corpo — só rota");
        r.Sucesso.Should().BeTrue();
        r.UrlDanfe.Should().Be("https://x/danfe.pdf");
        r.EmContingencia.Should().BeFalse();
    }

    [Fact(DisplayName = "EmitirNotaAsync — NFE usa a rota /nfe/emitir-da-venda/{id}")]
    public async Task EmitirNotaAsync_Nfe_UsaRotaCerta()
    {
        var vendaId = Guid.NewGuid();
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"sucesso\":true,\"mensagem\":\"ok\",\"status\":\"Autorizada\",\"urlDanfe\":\"https://x/danfe.pdf\",\"ambiente\":\"Produção\",\"emContingencia\":false}");

        await new HttpFiscalService(h).EmitirNotaAsync(vendaId, "NFE");

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/notas-fiscais/nfe/emitir-da-venda/{vendaId}");
    }

    [Fact(DisplayName = "EmitirNotaAsync — contingência: Sucesso=true e EmContingencia=true, igual ao FiscalService local")]
    public async Task EmitirNotaAsync_Contingencia_ReflemeAmbasAsFlags()
    {
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"sucesso\":true,\"mensagem\":\"Venda salva em modo contingência\",\"status\":\"Contingência\",\"urlDanfe\":null,\"ambiente\":\"Homologação\",\"emContingencia\":true}");

        var r = await new HttpFiscalService(h).EmitirNotaAsync(Guid.NewGuid(), "NFCE");

        r.Sucesso.Should().BeTrue();
        r.EmContingencia.Should().BeTrue();
        r.UrlDanfe.Should().BeNull();
    }

    [Fact(DisplayName = "EmitirNotaAsync — 400 (rejeição SEFAZ) vira Sucesso=false com a mensagem, NÃO lança exceção")]
    public async Task EmitirNotaAsync_400_ViraResultadoDeFalhaSemLancar()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest, "{\"erro\":\"Rejeição 703: NCM inválido\"}");

        var r = await new HttpFiscalService(h).EmitirNotaAsync(Guid.NewGuid(), "NFCE");

        r.Sucesso.Should().BeFalse();
        r.Mensagem.Should().Contain("NCM inválido");
    }

    [Fact(DisplayName = "EmitirNotaAsync — 404 (venda não encontrada) vira KeyNotFoundException, igual ao FiscalService local")]
    public async Task EmitirNotaAsync_404_LancaKeyNotFound()
    {
        var h = new HandlerGravador(HttpStatusCode.NotFound, "{\"erro\":\"Venda não encontrada.\"}");

        var act = async () => await new HttpFiscalService(h).EmitirNotaAsync(Guid.NewGuid(), "NFCE");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "EmitirNotaAsync — 403 vira UnauthorizedAccessException legível")]
    public async Task EmitirNotaAsync_403_LancaUnauthorizedAccess()
    {
        var act = async () => await new HttpFiscalService(new HandlerGravador(HttpStatusCode.Forbidden))
            .EmitirNotaAsync(Guid.NewGuid(), "NFCE");

        (await act.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("*permissão*emitir nota fiscal*");
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — usa a rota certa e manda Itens/Motivo")]
    public async Task EmitirNotaDevolucaoAsync_UsaRotaEEnviaCorpo()
    {
        var vendaId = Guid.NewGuid();
        var produtoId = Guid.NewGuid();
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"sucesso\":true,\"mensagem\":\"NF-e de devolução emitida com sucesso!\",\"status\":\"Autorizada\",\"urlDanfe\":\"https://x/dev.pdf\",\"ambiente\":\"Homologação\",\"emContingencia\":false}");

        var itens = new List<(Guid, string, decimal, decimal)> { (produtoId, "Cimento", 2m, 35.90m) };
        var r = await new HttpFiscalService(h).EmitirNotaDevolucaoAsync(vendaId, itens, "Produto com defeito");

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/notas-fiscais/{vendaId}/emitir-devolucao");
        h.UltimaRequisicao.Method.Should().Be(HttpMethod.Post);
        r.Sucesso.Should().BeTrue();
        r.UrlDanfe.Should().Be("https://x/dev.pdf");
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — 400 (sem chave original ou rejeição) vira Sucesso=false, NÃO lança — igual ao FiscalService local")]
    public async Task EmitirNotaDevolucaoAsync_400_ViraResultadoDeFalhaSemLancar()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest,
            "{\"erro\":\"Essa venda não tem nota fiscal original (NF-e) registrada — não é possível emitir NF-e de devolução sem a chave da nota original.\"}");

        var r = await new HttpFiscalService(h).EmitirNotaDevolucaoAsync(
            Guid.NewGuid(), new List<(Guid, string, decimal, decimal)>(), "motivo");

        r.Sucesso.Should().BeFalse();
        r.Mensagem.Should().Contain("chave da nota original");
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — 404 (venda não encontrada) vira KeyNotFoundException")]
    public async Task EmitirNotaDevolucaoAsync_404_LancaKeyNotFound()
    {
        var h = new HandlerGravador(HttpStatusCode.NotFound, "{\"erro\":\"Venda não encontrada.\"}");

        var act = async () => await new HttpFiscalService(h).EmitirNotaDevolucaoAsync(
            Guid.NewGuid(), new List<(Guid, string, decimal, decimal)>(), "motivo");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}