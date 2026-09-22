// ERP.Tests/WPF/HttpCaixaServiceTests.cs
using ERP.Application.DTOs;
using ERP.Domain.Enums;
using ERP.WPF.Services;
using ERP.WPF.State;
using FluentAssertions;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.WPF;

/// <summary>
/// Fase C, módulo Caixa — mesmo estilo dos outros HttpXServiceTests: handler
/// falso mínimo. Foco nos pontos específicos de Caixa: roteamento de
/// RegistrarMovimentoAsync por tipo (sangria/suprimento/movimento), o corpo
/// POST /abrir usando só ValorAbertura (não o DTO local inteiro — servidor
/// ignora UsuarioId/OperadorNome do corpo por design, S8 FIX), e
/// FecharCaixaAsync sem corpo.
/// </summary>
public class HttpCaixaServiceTests
{
    public HttpCaixaServiceTests() => AppSession.ApiBaseUrl = "http://localhost";

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

    private const string CaixaJson =
        "{\"id\":\"33333333-3333-3333-3333-333333333333\",\"numeroCaixa\":7,\"operadorNome\":\"Matheus\"," +
        "\"dataAbertura\":\"2026-09-22T08:00:00\",\"dataFechamento\":null,\"valorAbertura\":100.0," +
        "\"status\":\"Aberto\",\"movimentos\":[]}";

    private const string ResumoJson =
        "{\"caixaId\":\"33333333-3333-3333-3333-333333333333\",\"numeroCaixa\":7,\"operadorNome\":\"Matheus\"," +
        "\"dataAbertura\":\"2026-09-22T08:00:00\",\"dataFechamento\":null,\"status\":\"Aberto\"," +
        "\"saldoInicial\":100.0,\"vendasDinheiro\":50.0,\"vendasPix\":0,\"vendasCartaoDebito\":0," +
        "\"vendasCartaoCredito\":0,\"vendasAPrazo\":0,\"vendasHaver\":0,\"suprimentos\":0,\"sangrias\":0," +
        "\"despesas\":0,\"extrato\":[\"ABERTURA + R$ 100,00\"]}";

    [Fact(DisplayName = "ObterCaixaAbertoAsync — 404 vira null")]
    public async Task ObterCaixaAbertoAsync_404_RetornaNull()
        => (await new HttpCaixaService(new HandlerGravador(HttpStatusCode.NotFound)).ObterCaixaAbertoAsync(Guid.NewGuid()))
            .Should().BeNull();

    [Fact(DisplayName = "ObterCaixaAbertoAsync — desserializa CaixaDto")]
    public async Task ObterCaixaAbertoAsync_Encontrado_Desserializa()
    {
        var caixa = await new HttpCaixaService(new HandlerGravador(HttpStatusCode.OK, CaixaJson))
            .ObterCaixaAbertoAsync(Guid.NewGuid());
        caixa!.NumeroCaixa.Should().Be(7);
        caixa.OperadorNome.Should().Be("Matheus");
    }

    [Fact(DisplayName = "AbrirCaixaAsync — envia só ValorAbertura no corpo (servidor ignora Usuario/Operador do body)")]
    public async Task AbrirCaixaAsync_EnviaSoValorAbertura()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "{\"mensagem\":\"ok\"}");
        await new HttpCaixaService(h).AbrirCaixaAsync(new AbrirCaixaDto
        {
            UsuarioId = Guid.NewGuid(), OperadorNome = "Devia ser ignorado", ValorAbertura = 150m
        });

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/caixa/abrir");
        using var json = JsonDocument.Parse(h.UltimoCorpo!);
        json.RootElement.GetProperty("ValorAbertura").GetDecimal().Should().Be(150m);
        json.RootElement.TryGetProperty("UsuarioId", out _).Should().BeFalse(
            "o corpo não deve nem tentar mandar UsuarioId — a API ignora e resolve pelo JWT");
    }

    [Fact(DisplayName = "AbrirCaixaAsync — 400 vira InvalidOperationException (caixa já aberto)")]
    public async Task AbrirCaixaAsync_400_LancaInvalidOperationException()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest, "{\"erro\":\"Já existe um caixa aberto.\"}");
        var act = async () => await new HttpCaixaService(h).AbrirCaixaAsync(new AbrirCaixaDto { ValorAbertura = 0 });

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Já existe um caixa aberto*");
    }

    [Theory(DisplayName = "RegistrarMovimentoAsync — Sangria e Suprimento vão pras rotas dedicadas, resto vai pra /movimento")]
    [InlineData(TipoMovimentoCaixa.Sangria, "sangria")]
    [InlineData(TipoMovimentoCaixa.Suprimento, "suprimento")]
    [InlineData(TipoMovimentoCaixa.PagamentoDespesa, "movimento")]
    public async Task RegistrarMovimentoAsync_RoteiaPorTipo(TipoMovimentoCaixa tipo, string rotaEsperada)
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "{\"mensagem\":\"ok\"}");
        await new HttpCaixaService(h).RegistrarMovimentoAsync(
            Guid.NewGuid(), 50m, "teste", PaymentMethod.Dinheiro, tipo);

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be($"/api/caixa/{rotaEsperada}");
    }

    [Fact(DisplayName = "RegistrarMovimentoAsync — corpo manda Tipo/FormaPagamento como texto (não número)")]
    public async Task RegistrarMovimentoAsync_EnviaEnumComoTexto()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "{\"mensagem\":\"ok\"}");
        await new HttpCaixaService(h).RegistrarMovimentoAsync(
            Guid.NewGuid(), 50m, "teste", PaymentMethod.Pix, TipoMovimentoCaixa.Sangria);

        using var json = JsonDocument.Parse(h.UltimoCorpo!);
        json.RootElement.GetProperty("Tipo").GetString().Should().Be("Sangria");
        json.RootElement.GetProperty("FormaPagamento").GetString().Should().Be("Pix");
    }

    [Fact(DisplayName = "RegistrarMovimentoAsync — 400 vira InvalidOperationException (ex.: sangria maior que o saldo)")]
    public async Task RegistrarMovimentoAsync_400_LancaInvalidOperationException()
    {
        var h = new HandlerGravador(HttpStatusCode.BadRequest, "{\"erro\":\"Valor maior que o saldo em caixa.\"}");
        var act = async () => await new HttpCaixaService(h)
            .RegistrarMovimentoAsync(Guid.NewGuid(), 99999m, "x", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*maior que o saldo*");
    }

    [Fact(DisplayName = "RegistrarMovimentoAsync — 403 vira UnauthorizedAccessException legível")]
    public async Task RegistrarMovimentoAsync_403_LancaUnauthorizedAccess()
    {
        var act = async () => await new HttpCaixaService(new HandlerGravador(HttpStatusCode.Forbidden))
            .RegistrarMovimentoAsync(Guid.NewGuid(), 10m, "x", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact(DisplayName = "FecharCaixaAsync — POST sem corpo pra /fechar")]
    public async Task FecharCaixaAsync_PostSemCorpo()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, "{\"mensagem\":\"ok\"}");
        await new HttpCaixaService(h).FecharCaixaAsync(Guid.NewGuid());

        h.UltimaRequisicao!.RequestUri!.AbsolutePath.Should().Be("/api/caixa/fechar");
        h.UltimaRequisicao.Method.Should().Be(HttpMethod.Post);
    }

    [Fact(DisplayName = "ObterResumoAsync — formata a data como yyyy-MM-dd na query string")]
    public async Task ObterResumoAsync_FormataDataNaQuery()
    {
        var h = new HandlerGravador(HttpStatusCode.OK, ResumoJson);
        await new HttpCaixaService(h).ObterResumoAsync(Guid.NewGuid(), new DateTime(2026, 3, 5));

        h.UltimaRequisicao!.RequestUri!.Query.Should().Be("?data=2026-03-05");
    }

    [Fact(DisplayName = "ObterResumoAsync — 404 vira null (nenhum caixa nessa data)")]
    public async Task ObterResumoAsync_404_RetornaNull()
        => (await new HttpCaixaService(new HandlerGravador(HttpStatusCode.NotFound))
            .ObterResumoAsync(Guid.NewGuid(), DateTime.Today)).Should().BeNull();

    [Fact(DisplayName = "ObterResumoAsync — desserializa totais e extrato")]
    public async Task ObterResumoAsync_Desserializa()
    {
        var resumo = await new HttpCaixaService(new HandlerGravador(HttpStatusCode.OK, ResumoJson))
            .ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        resumo!.SaldoInicial.Should().Be(100m);
        resumo.VendasDinheiro.Should().Be(50m);
        resumo.Extrato.Should().ContainSingle();
    }

    [Fact(DisplayName = "ExisteMovimentoParaSalePaymentAsync — não tem endpoint, lança NotSupportedException")]
    public async Task ExisteMovimentoParaSalePaymentAsync_LancaNotSupported()
    {
        var act = async () => await new HttpCaixaService().ExisteMovimentoParaSalePaymentAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
