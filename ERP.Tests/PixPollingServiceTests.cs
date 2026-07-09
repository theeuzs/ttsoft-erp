using ERP.WPF.Helpers;
using FluentAssertions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests;

// S15 FIX — regressão: PixPollingService.PollingLoop tinha catch mudo (catch {}),
// que escondia qualquer erro (rede instável, token expirado, contrato de API
// mudado, bug de parsing) sem deixar rastro nenhum. O fix trocou o catch mudo
// por um callback de erro (produção usa Log.Warning via Serilog, igual antes).
// Pra tornar isso testável sem esperar os 5s reais e sem bater numa API de
// verdade, o handler HTTP, o intervalo de polling e o callback de erro viraram
// injetáveis via construtor — todos opcionais, com default que preserva o
// comportamento de produção exatamente como era (WPF continua chamando
// "new PixPollingService()" sem nenhum argumento).
public class PixPollingServiceTests
{
    private class ThrowingHandler : HttpMessageHandler
    {
        public int Chamadas;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Chamadas);
            return Task.FromException<HttpResponseMessage>(
                new HttpRequestException("Falha de rede simulada (teste)"));
        }
    }

    [Fact(DisplayName =
        "S15 — falha na verificação chama o callback de erro e o loop continua rodando")]
    public async Task PollingLoop_FalhaNaVerificacao_ChamaCallbackEContinuaRodando()
    {
        var chamadas = new ConcurrentBag<(Exception Ex, string Txid, string Provedor)>();
        var handler  = new ThrowingHandler();

        using var service = new PixPollingService(
            handler: handler,
            intervalo: TimeSpan.FromMilliseconds(20),
            onErro: (ex, txid, provedor) => chamadas.Add((ex, txid, provedor)));

        service.IniciarPolling("txid-teste-123", "token-fake", "openpix");

        // S15 FIX (robustez do teste): em vez de um Task.Delay fixo (frágil —
        // já flakeou uma vez nessa mesma sessão, provavelmente por JIT/thread pool
        // ainda "frios" na primeira execução do processo de teste), espera ativamente
        // até ver pelo menos 2 chamadas capturadas, com um teto de 5s bem folgado.
        // Isso torna o teste determinístico: ou a funcionalidade funciona (passa
        // rápido, tipicamente bem antes de 1s) ou realmente não funciona (falha
        // de verdade após o teto, não por falta de sorte no timing).
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        while (chamadas.Count < 2 && cronometro.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(20);

        service.Parar();

        chamadas.Should().NotBeEmpty(
            $"cada falha de verificação deve disparar o callback de erro, não desaparecer " +
            $"silenciosamente como acontecia com o catch mudo original (handler.Chamadas={handler.Chamadas} " +
            "— se 0 aqui, o loop nunca chegou a chamar o HTTP dentro do teto de 5s)");
        chamadas.Should().HaveCountGreaterThan(1,
            "o loop deve continuar rodando após uma falha — não pode morrer no primeiro erro");

        var primeira = chamadas.First();
        primeira.Txid.Should().Be("txid-teste-123");
        primeira.Provedor.Should().Be("openpix");
        primeira.Ex.Should().BeOfType<HttpRequestException>(
            "a exceção real deve chegar ao callback, não ser substituída ou perdida no caminho");
    }

    [Fact(DisplayName = "S15 — callback padrão (produção) não lança exceção ao processar um erro")]
    public void DefaultOnErro_CaminhoDeProducao_NaoLancaExcecao()
    {
        // Construção sem argumentos = exatamente o que o WPF já faz em produção
        // (new PixPollingService(), sem parâmetros). Confirma que o caminho padrão
        // (Log.Warning via Serilog) não lança mesmo sem nenhum sink configurado —
        // Serilog sem CreateLogger() configurado é um logger nulo seguro.
        using var service = new PixPollingService();

        var metodo = typeof(PixPollingService).GetMethod(
            "DefaultOnErro", BindingFlags.NonPublic | BindingFlags.Static);
        metodo.Should().NotBeNull("DefaultOnErro deve existir como método privado estático");

        var act = () =>
        {
            metodo!.Invoke(null, new object[]
            {
                new InvalidOperationException("erro simulado"), "txid-x", "gerencianet"
            });
        };

        act.Should().NotThrow();
    }
}