// ERP.Tests/WPF/HttpContaReceberServiceTests.cs
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
/// Fase C, módulo 4 (Contas a Receber) — mesmo estilo dos outros
/// HttpXServiceTests. Foco nos 2 métodos achados auditando ANTES de migrar
/// (ExisteContaParaSalePaymentAsync/GerarContaAPrazoAsync, usados pelo
/// MotorFinanceiroService no WPF — a mesma lição do módulo Caixa, dessa vez
/// aplicada antes de quebrar em produção), nas duas serializações
/// arriscadas (ResumoAsync devolve tupla; GerarBoletoAsync mapeia status
/// HTTP pro enum GerarBoletoStatus), e nos 2 métodos sem endpoint
/// (confirmadamente sem uso real).
/// </summary>
public class HttpContaReceberServiceTests
{
    public HttpContaReceberServiceTests() => AppSession.ApiBaseUrl = "http://localhost";

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

    [Fact(DisplayName = "GerarContaAPrazoAsync — usa a rota certa e manda os campos certos")]
    public async Task GerarContaAPrazoAsync_UsaRotaEEnviaCampos()
    {
        var h = new HandlerGravador(HttpStatusCode.OK);
        var clienteId = Guid.NewGuid();
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();

        await new HttpContaReceberService(h).GerarContaAPrazoAsync(clienteId, vendaId, 150m, "Venda a prazo", salePaymentId);

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/contas-receber/gerar-a-prazo");
        using var json = JsonDocument.Parse(h.UltimoCorpo!);
        json.RootElement.GetProperty("ClienteId").GetGuid().Should().Be(clienteId);
        json.RootElement.GetProperty("VendaId").GetGuid().Should().Be(vendaId);
        json.RootElement.GetProperty("Valor").GetDecimal().Should().Be(150m);
        json.RootElement.GetProperty("SalePaymentId").GetGuid().Should().Be(salePaymentId);
    }

    [Fact(DisplayName = "ExisteContaParaSalePaymentAsync — usa a rota certa, desserializa bool")]
    public async Task ExisteContaParaSalePaymentAsync_UsaRotaCerta()
    {
        var salePaymentId = Guid.NewGuid();
        var h = new HandlerGravador(HttpStatusCode.OK, "true");

        (await new HttpContaReceberService(h).ExisteContaParaSalePaymentAsync(salePaymentId)).Should().BeTrue();
        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/contas-receber/existe-para-sale-payment/{salePaymentId}");
    }

    [Fact(DisplayName = "CountInadimplentesAsync — usa a rota certa, desserializa int")]
    public async Task CountInadimplentesAsync_UsaRotaCerta()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "7");
        (await new HttpContaReceberService(h).CountInadimplentesAsync()).Should().Be(7);
        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/contas-receber/inadimplentes/count");
    }

    [Fact(DisplayName = "GetResumoAsync — reconstrói a tupla a partir do JSON (controller devolve objeto, não ValueTuple cru)")]
    public async Task GetResumoAsync_ReconstroiTupla()
    {
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"totalPendente\":500.00,\"totalVencido\":120.50,\"qtdClientes\":3}");

        var (pendente, vencido, qtd) = await new HttpContaReceberService(h).GetResumoAsync();

        pendente.Should().Be(500.00m);
        vencido.Should().Be(120.50m);
        qtd.Should().Be(3);
    }

    [Fact(DisplayName = "GetBySaleIdAsync — sem endpoint, lança NotSupportedException")]
    public async Task GetBySaleIdAsync_LancaNotSupported()
    {
        var act = async () => await new HttpContaReceberService().GetBySaleIdAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact(DisplayName = "GetParcelasByVendaAsync — sem endpoint, lança NotSupportedException")]
    public async Task GetParcelasByVendaAsync_LancaNotSupported()
    {
        var act = async () => await new HttpContaReceberService().GetParcelasByVendaAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact(DisplayName = "DarDescontoAsync — 404 vira KeyNotFoundException, 400 vira InvalidOperationException")]
    public async Task DarDescontoAsync_MapeiaErrosCorretamente()
    {
        var svc404 = new HttpContaReceberService(new HandlerGravador(HttpStatusCode.NotFound, "{\"erro\":\"não achou\"}"));
        await ((Func<Task>)(() => svc404.DarDescontoAsync(Guid.NewGuid(), 10m, "motivo")))
            .Should().ThrowAsync<KeyNotFoundException>();

        var svc400 = new HttpContaReceberService(new HandlerGravador(HttpStatusCode.BadRequest, "{\"erro\":\"conta já cancelada\"}"));
        var act400 = async () => await svc400.DarDescontoAsync(Guid.NewGuid(), 10m, "motivo");
        (await act400.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*conta já cancelada*");
    }

    [Fact(DisplayName = "DarBaixaEmLoteAsync — envia lista de Ids corretamente")]
    public async Task DarBaixaEmLoteAsync_EnviaListaDeIds()
    {
        var h = new HandlerGravador(HttpStatusCode.OK);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await new HttpContaReceberService(h).DarBaixaEmLoteAsync(ids, 200m, 10m, "Dinheiro");

        using var json = JsonDocument.Parse(h.UltimoCorpo!);
        json.RootElement.GetProperty("ContaIds").GetArrayLength().Should().Be(2);
        json.RootElement.GetProperty("ValorAPagar").GetDecimal().Should().Be(200m);
    }

    [Fact(DisplayName = "GerarBoletoAsync — 404 vira ContaNaoEncontrada")]
    public async Task GerarBoletoAsync_404_ViraContaNaoEncontrada()
    {
        var r = await new HttpContaReceberService(new HandlerGravador(HttpStatusCode.NotFound))
            .GerarBoletoAsync(Guid.NewGuid());
        r.Status.Should().Be(ERP.Application.Interfaces.GerarBoletoStatus.ContaNaoEncontrada);
    }

    [Fact(DisplayName = "GerarBoletoAsync — 503 vira AsaasIndisponivel")]
    public async Task GerarBoletoAsync_503_ViraAsaasIndisponivel()
    {
        var r = await new HttpContaReceberService(new HandlerGravador(HttpStatusCode.ServiceUnavailable, "{\"erro\":\"sem chave configurada\"}"))
            .GerarBoletoAsync(Guid.NewGuid());
        r.Status.Should().Be(ERP.Application.Interfaces.GerarBoletoStatus.AsaasIndisponivel);
        r.Erro.Should().Contain("sem chave configurada");
    }

    [Fact(DisplayName = "GerarBoletoAsync — 200 desserializa BoletoUrl/InvoiceUrl/BoletoBarCode")]
    public async Task GerarBoletoAsync_Sucesso_Desserializa()
    {
        var h = new HandlerGravador(HttpStatusCode.OK,
            "{\"boletoUrl\":\"https://x/boleto.pdf\",\"invoiceUrl\":\"https://x/fatura\",\"boletoBarCode\":\"123\",\"asaasStatus\":\"PENDING\"}");

        var r = await new HttpContaReceberService(h).GerarBoletoAsync(Guid.NewGuid());

        r.Status.Should().Be(ERP.Application.Interfaces.GerarBoletoStatus.Sucesso);
        r.BoletoUrl.Should().Be("https://x/boleto.pdf");
        r.BoletoBarCode.Should().Be("123");
    }
}
