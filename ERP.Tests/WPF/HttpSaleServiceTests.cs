// ERP.Tests/WPF/HttpSaleServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.Exceptions;
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
/// Fase B da migração WPF→API (08/2026) — testa HttpSaleService de forma
/// COMPORTAMENTAL (não de implementação interna), como combinado com o GPT:
/// não é preciso testar os 6 métodos individualmente pra provar que o
/// tratamento de 401/400/500 funciona — cobre CreateAsync (onde já existia
/// tratamento de 400 antes dessa mudança, então também serve de teste de
/// regressão) e confirma o comportamento nos outros pontos importantes.
///
/// Usa um HttpMessageHandler falso mínimo, não uma biblioteca de mock HTTP —
/// decisão explícita da revisão cruzada, pra não introduzir dependência nova
/// só pra isso.
///
/// CORREÇÃO (achado do GPT na 1ª rodada): AppSession.ApiBaseUrl PRECISA
/// estar preenchido — HttpSaleService monta a URL concatenando
/// $"{AppSession.ApiBaseUrl}/api/sales..." antes de mandar pro HttpClient,
/// não usa BaseAddress do HttpClient. Com ApiBaseUrl vazio (padrão), a
/// string vira só "/api/sales" — sem esquema, HttpClient recusa como URI
/// relativa mesmo com um handler configurado, e a requisição nunca chega no
/// RespostaFixaHandler. O valor em si não importa (o handler falso ignora
/// host/caminho, só devolve a resposta configurada) — só precisa ser uma
/// URI absoluta válida. Setado uma vez no construtor (roda antes de CADA
/// teste, convenção do xUnit), não precisa repetir em cada método.
/// Confirmado seguro: nenhum outro teste do projeto toca ApiBaseUrl
/// especificamente (só Login/Logout, que mexem em outros campos).
/// </summary>
public class HttpSaleServiceTests
{
    public HttpSaleServiceTests()
    {
        AppSession.ApiBaseUrl = "http://localhost";
    }

    /// <summary>Handler mínimo que sempre devolve a resposta configurada,
    /// não importa qual requisição chegou — suficiente pra testar como
    /// HttpSaleService INTERPRETA um código de status, sem precisar de uma
    /// API real (nem falsa) do outro lado.</summary>
    private sealed class RespostaFixaHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string? _corpoJson;

        public RespostaFixaHandler(HttpStatusCode status, string? corpoJson = null)
        {
            _status = status;
            _corpoJson = corpoJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(_status);
            if (_corpoJson != null)
                resp.Content = new StringContent(_corpoJson, Encoding.UTF8, "application/json");
            return Task.FromResult(resp);
        }
    }

    private static CreateSaleDto DtoDeVendaMinima() => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = Guid.NewGuid(),
        Items = { new CreateSaleItemDto { ProductId = Guid.NewGuid(), Quantity = 1m, UnitPrice = 10m } }
    };

    [Fact(DisplayName = "CreateAsync — 401 vira SessaoExpiradaException, nunca HttpRequestException genérica")]
    public async Task CreateAsync_Com401_LancaSessaoExpirada()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.Unauthorized));

        var act = async () => await service.CreateAsync(DtoDeVendaMinima());

        await act.Should().ThrowAsync<SessaoExpiradaException>(
            "401 significa token vazio/expirado/inválido — nunca deveria parecer 'sem internet' pro classificador de conectividade");
    }

    [Fact(DisplayName = "CreateAsync — 400 com {erro} continua virando InvalidOperationException (regressão)")]
    public async Task CreateAsync_Com400_AindaLancaInvalidOperationException()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(
            HttpStatusCode.BadRequest, "{\"erro\":\"Não é possível realizar vendas: O CAIXA ESTÁ FECHADO.\"}"));

        var act = async () => await service.CreateAsync(DtoDeVendaMinima());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*CAIXA ESTÁ FECHADO*",
                "esse comportamento já existia antes da mudança de 401 — não pode ter sido atropelado");
    }

    [Fact(DisplayName = "CreateAsync — 500 NÃO vira SessaoExpiradaException")]
    public async Task CreateAsync_Com500_NaoLancaSessaoExpirada()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.InternalServerError));

        var act = async () => await service.CreateAsync(DtoDeVendaMinima());

        var excecao = await act.Should().ThrowAsync<Exception>();
        excecao.Which.Should().NotBeOfType<SessaoExpiradaException>(
            "erro de servidor não é sessão expirada — não pode ser confundido com 401");
    }

    [Fact(DisplayName = "CreateAsync — 500 vira HttpRequestException, e continua subindo sem ser capturada aqui dentro")]
    public async Task CreateAsync_Com500_LancaHttpRequestException_QueContinuaSubindo()
    {
        // Prova que HttpSaleService não engole a exceção de infraestrutura —
        // é o ConnectivityExceptionClassifier (fora dessa classe) quem decide
        // se isso vira modo offline, não HttpSaleService.
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.ServiceUnavailable));

        var act = async () => await service.CreateAsync(DtoDeVendaMinima());

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact(DisplayName = "GetDetailAsync — 401 vira SessaoExpiradaException mesmo no caminho que trata 404 como null")]
    public async Task GetDetailAsync_Com401_LancaSessaoExpirada()
    {
        // Confirma que o helper de 401 roda ANTES da lógica específica de
        // cada método (aqui, "404 vira null") — 401 nunca deveria ser
        // silenciosamente interpretado como "venda não encontrada".
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.Unauthorized));

        var act = async () => await service.GetDetailAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<SessaoExpiradaException>();
    }

    [Fact(DisplayName = "CancelAsync — 401 vira SessaoExpiradaException, não KeyNotFoundException")]
    public async Task CancelAsync_Com401_LancaSessaoExpirada()
    {
        // Confirma que 401 tem prioridade sobre a lógica de 404→KeyNotFoundException
        // já existente em CancelAsync — os dois são erros HTTP diferentes,
        // não podem ser confundidos.
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.Unauthorized));

        var act = async () => await service.CancelAsync(Guid.NewGuid(), "motivo qualquer");

        await act.Should().ThrowAsync<SessaoExpiradaException>();
    }

    [Fact(DisplayName = "CancelAsync — 404 continua KeyNotFoundException (regressão, 401 não afetou esse caminho)")]
    public async Task CancelAsync_Com404_AindaLancaKeyNotFoundException()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.NotFound));

        var act = async () => await service.CancelAsync(Guid.NewGuid(), "motivo qualquer");

        await act.Should().ThrowAsync<System.Collections.Generic.KeyNotFoundException>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Achado numa auditoria pós-troca de DI (13/08, sem o GPT disponível pra
    // revisar em conjunto) — os 3 métodos abaixo (AtualizarDadosNfceAsync,
    // GetAllAsync, GetSalesReportAsync) nunca tinham tido teste NENHUM, nem
    // de erro nem de sucesso. AtualizarDadosNfceAsync em particular é usado
    // pelo subsistema fiscal (NfeContingencyWorker/FiscalService/
    // NotasFiscaisViewModel), que já está rodando em produção real na Vila
    // Verde — cobertura zero nesse método específico era um risco real, não
    // teórico. Fechando a lacuna aqui.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "AtualizarDadosNfceAsync — 401 vira SessaoExpiradaException")]
    public async Task AtualizarDadosNfceAsync_Com401_LancaSessaoExpirada()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.Unauthorized));

        var act = async () => await service.AtualizarDadosNfceAsync(
            Guid.NewGuid(), "https://danfe.teste/x", "Autorizada", "homologacao", "ref-123");

        await act.Should().ThrowAsync<SessaoExpiradaException>(
            "sem isso, o subsistema fiscal (NfeContingencyWorker) ficaria em loop de 401 sem nunca perceber que é sessão, não rede");
    }

    [Fact(DisplayName = "AtualizarDadosNfceAsync — sucesso (204) completa sem lançar nada")]
    public async Task AtualizarDadosNfceAsync_ComSucesso_NaoLancaNada()
    {
        // Nunca tinha sido testado no caminho feliz — só confirmava
        // comportamento de erro antes dessa auditoria. O método é void
        // (Task), então "não lançar" É o comportamento observável de sucesso.
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.NoContent));

        var act = async () => await service.AtualizarDadosNfceAsync(
            Guid.NewGuid(), "https://danfe.teste/x", "Autorizada", "homologacao", "ref-123");

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "GetAllAsync — 401 vira SessaoExpiradaException")]
    public async Task GetAllAsync_Com401_LancaSessaoExpirada()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.Unauthorized));

        var act = async () => await service.GetAllAsync();

        await act.Should().ThrowAsync<SessaoExpiradaException>();
    }

    [Fact(DisplayName = "GetAllAsync — sucesso desserializa a lista corretamente (nunca confirmado antes)")]
    public async Task GetAllAsync_ComSucesso_DesserializaListaCorretamente()
    {
        var vendaId = Guid.NewGuid();
        var corpo = $"[{{\"id\":\"{vendaId}\",\"saleNumber\":\"PDV-001\",\"customerName\":\"Cliente Teste\"," +
                    "\"sellerName\":\"Vendedor\",\"saleDate\":\"2026-08-13T10:00:00\",\"status\":\"SemNota\"," +
                    "\"paymentMethods\":\"Dinheiro\",\"total\":150.50}]";
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.OK, corpo));

        var resultado = await service.GetAllAsync();

        resultado.Should().ContainSingle();
        resultado.First().Id.Should().Be(vendaId, "prova que a desserialização (camelCase da API → propriedades PascalCase do DTO) funciona de verdade, não só que não lançou exceção");
        resultado.First().Total.Should().Be(150.50m);
    }

    [Fact(DisplayName = "GetSalesReportAsync — 401 vira SessaoExpiradaException")]
    public async Task GetSalesReportAsync_Com401_LancaSessaoExpirada()
    {
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.Unauthorized));

        var act = async () => await service.GetSalesReportAsync(DateTime.Today.AddDays(-30), DateTime.Today);

        await act.Should().ThrowAsync<SessaoExpiradaException>();
    }

    [Fact(DisplayName = "GetSalesReportAsync — sucesso desserializa corretamente (nunca confirmado antes)")]
    public async Task GetSalesReportAsync_ComSucesso_DesserializaCorretamente()
    {
        var corpo = "[{\"dataVenda\":\"2026-08-13T10:00:00\",\"numeroRecibo\":\"PDV-001\"," +
                    "\"clienteNome\":\"Cliente Teste\",\"vendedorNome\":\"Vendedor\"," +
                    "\"formaPagamento\":\"Dinheiro\",\"valorTotal\":150.50}]";
        var service = new HttpSaleService(new RespostaFixaHandler(HttpStatusCode.OK, corpo));

        var resultado = await service.GetSalesReportAsync(DateTime.Today.AddDays(-30), DateTime.Today);

        resultado.Should().ContainSingle();
        resultado.First().ValorTotal.Should().Be(150.50m);
        resultado.First().NumeroRecibo.Should().Be("PDV-001");
    }
}