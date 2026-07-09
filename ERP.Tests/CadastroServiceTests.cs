using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
//  S15 FIX — regressão: circuit breaker da BrasilAPI deixava cadastro sem
//  e-mail de ativação (CadastroService.EnviarEmailConfirmacaoAsync(emailRfbConfirmacao!, ...)
//  com emailRfbConfirmacao == null quando o circuit está aberto). O cliente
//  ficava com a conta INATIVA e sem nenhum caminho automático de ativação,
//  sem nenhum aviso, durante qualquer outage da BrasilAPI.
// ═══════════════════════════════════════════════════════════════════════════════
public class CadastroServiceTests
{
    // Stub simples de HttpMessageHandler — sempre responde com o status configurado.
    private class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_status));
    }

    private static AppDbContext CriarContexto(string dbName, Guid tenantId)
    {
        var tenant = new FakeRequestTenant { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options, tenant);
    }

    [Fact(DisplayName =
        "S15 — Circuit breaker aberto: cadastro cria conta inativa sem tentar e-mail quebrado, " +
        "e avisa sobre ativação manual")]
    public async Task CadastrarTenantAsync_CircuitBreakerAberto_NaoQuebraEAvisaAtivacaoManual()
    {
        // Arrange: força o circuit breaker a abrir de verdade (5 falhas consecutivas),
        // usando o BrasilApiService real — não um mock — porque o bug está no
        // tratamento do estado real do circuit breaker, não numa interface.
        using var httpFalha = new HttpClient(new StubHandler(HttpStatusCode.ServiceUnavailable));
        var brasilApi = new BrasilApiService(httpFalha);

        for (int i = 0; i < 5; i++)
            await brasilApi.ConsultarCnpjAsync("11222333000181");

        brasilApi.CircuitAberto.Should().BeTrue(
            "5 falhas consecutivas devem abrir o circuit breaker — pré-condição do teste");

        try
        {
            var tenantId = Guid.NewGuid();
            using var db = CriarContexto($"cadastro_circuit_{Guid.NewGuid():N}", tenantId);

            var usersMock = new Mock<IUserRepository>();
            usersMock.Setup(u => u.GetByUsernameAndTenantAsync("admin", It.IsAny<Guid>()))
                .ReturnsAsync((User?)null);

            User? usuarioCriado = null;
            usersMock.Setup(u => u.AddAsync(It.IsAny<User>()))
                .Callback<User>(u => usuarioCriado = u)
                .Returns(Task.CompletedTask);

            var uowMock = new Mock<IUnitOfWork>();
            uowMock.Setup(u => u.Users).Returns(usersMock.Object);
            uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

            // Config vazia — sem SMTP configurado, igual ambiente de teste/dev real.
            // Isso já era assim antes do fix; o bug original não dependia de SMTP
            // configurado para existir (a falha era passar null pro parâmetro,
            // não o que acontece depois dentro do envio).
            var config = new ConfigurationBuilder().Build();

            var service = new CadastroService(uowMock.Object, db, config, brasilApi);

            var dto = new CadastroRequestDto
            {
                Cnpj        = "11222333000181",
                RazaoSocial = "Empresa Teste Circuit",
                Email       = "dono@empresateste.com",
                Senha       = "SenhaForte123"
            };

            // Act — antes do fix, isso corria o risco de nunca entregar e-mail
            // de ativação (bug silencioso: só se manifestava com SMTP configurado
            // em produção, nunca em dev/teste com config vazia — por isso nunca
            // foi pego antes).
            var resultado = await service.CadastrarTenantAsync(dto);

            // Assert — mensagem correta (não promete e-mail que não sai)
            resultado.MensagemSucesso.Should().Contain("ativar sua conta manualmente",
                "a mensagem deve avisar sobre ativação manual, não prometer um e-mail automático " +
                "que nunca teria como sair (não há e-mail RFB verificado nesse caminho)");
            resultado.MensagemSucesso.Should().NotContain("verifique o e-mail",
                "essa é a mensagem do caminho de divergência real de e-mail RFB — não deve aparecer " +
                "aqui, onde a BrasilAPI nem respondeu e não há e-mail RFB confirmado");

            // Assert — conta criada corretamente inativa, com token para ativação manual
            usuarioCriado.Should().NotBeNull();
            usuarioCriado!.IsActive.Should().BeFalse(
                "conta deve ficar inativa até verificação manual, mesma proteção do caso de divergência de e-mail");
            usuarioCriado.ConfirmacaoToken.Should().NotBeNullOrEmpty(
                "token deve existir para permitir ativação manual via suporte, mesmo sem e-mail automático");
        }
        finally
        {
            // Reset via reflection — ver ResetCircuitState() e o comentário lá
            // sobre por que uma chamada HTTP de sucesso não funciona aqui.
            ResetCircuitState();
        }
    }

    // S15 FIX (teste): resetar via uma chamada HTTP de sucesso/404 NÃO funciona —
    // ConsultarCnpjAsync verifica "if (DateTime.UtcNow < circuitoAbertoAte)" e
    // retorna cedo, ANTES de sequer tentar a chamada HTTP, enquanto o circuito
    // ainda está dentro da janela de 10 min. Único jeito limpo, sem expor um
    // método de reset só pra teste em produção: reflection nos campos estáticos.
    private static void ResetCircuitState()
    {
        var tipo = typeof(BrasilApiService);
        tipo.GetField("_falhasConsecutivas", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, 0);
        tipo.GetField("_circuitAbertoAte", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, DateTime.MinValue);
    }

    // S15 FIX — regressão de concorrência: _falhasConsecutivas++ era um
    // read-modify-write não-atômico. Corrigido com lock (ver BrasilApiService.cs).
    // Esse teste dispara N chamadas concorrentes que falham, exatamente no limiar
    // (ThresholdFalhas=5), e repete o cenário várias vezes: se o incremento
    // perdesse contagens sob concorrência, o circuito deixaria de abrir de forma
    // intermitente — corrida de dados são probabilísticas, então rodar uma vez só
    // não prova nada; repetir o cenário-limite N vezes é o jeito de expor isso.
    [Fact(DisplayName =
        "S15 — thread-safety: falhas concorrentes no limiar abrem o circuito de forma consistente")]
    public async Task ConsultarCnpjAsync_FalhasConcorrentesNoLimiar_AbreCircuitoDeFormaConsistente()
    {
        const int thresholdFalhas = 5; // ThresholdFalhas é private const em BrasilApiService
        const int repeticoes = 30;

        for (int r = 0; r < repeticoes; r++)
        {
            ResetCircuitState();

            using var http = new HttpClient(new StubHandler(HttpStatusCode.ServiceUnavailable));
            var service = new BrasilApiService(http);

            var tasks = Enumerable.Range(0, thresholdFalhas)
                .Select(_ => service.ConsultarCnpjAsync("11222333000181"));
            await Task.WhenAll(tasks);

            service.CircuitAberto.Should().BeTrue(
                $"repetição {r + 1}/{repeticoes}: exatamente {thresholdFalhas} falhas concorrentes " +
                "devem abrir o circuito de forma consistente — falha intermitente aqui indicaria " +
                "incremento perdendo contagem sob concorrência (a regressão que o lock corrige)");
        }

        ResetCircuitState();
    }

    // S15 FIX — regressão: e-mails devem sair mascarados nos logs (MascararEmail
    // em CadastroService), não em texto puro. MascararEmail é private static —
    // testado via reflection por ser função pura, sem dependências.
    [Theory(DisplayName = "S15 — MascararEmail oculta a parte local do e-mail, mantém o domínio")]
    [InlineData("joao.silva@gmail.com", "j***@gmail.com")]
    [InlineData("a@dominio.com.br", "a***@dominio.com.br")]
    [InlineData(null, "(vazio)")]
    [InlineData("", "(vazio)")]
    [InlineData("semarroba", "***")]
    public void MascararEmail_OcultaParteLocal_MantemDominio(string? entrada, string esperado)
    {
        var metodo = typeof(CadastroService).GetMethod(
            "MascararEmail", BindingFlags.NonPublic | BindingFlags.Static);
        metodo.Should().NotBeNull("MascararEmail deve existir como método privado estático em CadastroService");

        var resultado = (string)metodo!.Invoke(null, new object?[] { entrada })!;

        resultado.Should().Be(esperado);
    }
}