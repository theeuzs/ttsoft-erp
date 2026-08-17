// ERP.Tests/Application/ConnectivityExceptionClassifierTests.cs
using ERP.Application.Exceptions;
using ERP.Application.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// Fase 2 do Offline-First — testa o classificador ANTES de ligá-lo no
/// FinalizarVendaViewModel (passo 4 do plano combinado com GPT/Gemini).
/// O ponto central: SaleService.CreateAsync embrulha tudo (inclusive erro de
/// negócio) numa Exception genérica com a causa real em InnerException — os
/// testes usam exatamente esse formato embrulhado, não a exceção "crua".
/// </summary>
public class ConnectivityExceptionClassifierTests
{
    // SqlException é sealed, sem construtor público — GetUninitializedObject
    // é o jeito padrão de instanciar pra teste (bypassa o construtor; a
    // classificação aqui é por TIPO, não por propriedade da exceção, então
    // uma instância "vazia" já é suficiente pro que estamos testando).
    private static SqlException CriarSqlExceptionFake()
        => (SqlException)FormatterServices.GetUninitializedObject(typeof(SqlException));

    [Fact]
    public void LimiteCreditoExcedido_EmbrulhadoComoOSaleServiceFaz_NaoEhConectividade()
    {
        var causaReal = new LimiteCreditoExcedidoException("Cliente X", 100m, 50m, 30m);
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", causaReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeFalse(
            "limite de crédito é regra de negócio — tem que aparecer pro operador na hora, nunca virar venda offline");
    }

    [Fact]
    public void ProdutoNaoEncontrado_NaoEhConectividade()
    {
        var causaReal = new KeyNotFoundException("Produto X não encontrado.");
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", causaReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeFalse();
    }

    [Fact]
    public void CaixaFechado_NaoEhConectividade()
    {
        var causaReal = new InvalidOperationException("Não é possível realizar vendas: O CAIXA ESTÁ FECHADO.");
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", causaReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeFalse();
    }

    [Fact]
    public void SqlException_EmbrulhadaComoOSaleServiceFaz_EhConectividade()
    {
        var causaReal = CriarSqlExceptionFake();
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", causaReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeTrue(
            "falha real de SQL Server (rede caiu, servidor inacessível) tem que cair pro modo offline");
    }

    [Fact]
    public void TimeoutException_EhConectividade()
    {
        var causaReal = new TimeoutException("Timeout expired.");
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", causaReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeTrue();
    }

    [Fact]
    public void DbUpdateException_CausadaPorSqlException_EhConectividade()
    {
        var sqlEx = CriarSqlExceptionFake();
        var dbUpdateEx = new DbUpdateException("Erro ao salvar", sqlEx);
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", dbUpdateEx);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeTrue(
            "EF Core costuma embrulhar SqlException dentro de DbUpdateException — o classificador precisa olhar dentro dela também");
    }

    [Fact]
    public void DbUpdateException_SemCausaDeConectividade_NaoEhConectividade()
    {
        // Ex: violação de constraint por dado errado, não por rede.
        var dbUpdateEx = new DbUpdateException("Violação de constraint", new InvalidOperationException("dado inválido"));
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", dbUpdateEx);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeFalse();
    }

    [Fact]
    public void CadeiaComTresNiveis_AindaEncontraSqlExceptionNoFundo()
    {
        // Achado da revisão cruzada com GPT: a cadeia pode ter mais de um
        // nível de embrulho. Simula Exception → Exception → DbUpdateException
        // → SqlException — uma camada a mais do que o caso simples já
        // testado, provando que o classificador não assume profundidade fixa.
        var sqlEx = CriarSqlExceptionFake();
        var dbUpdateEx = new DbUpdateException("Erro ao salvar", sqlEx);
        var camadaExtra = new Exception("Camada intermediária qualquer", dbUpdateEx);
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", camadaExtra);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeTrue();
    }

    [Fact]
    public void ExcecaoDesconhecida_NuncaViraOfflineSozinha()
    {
        // Decisão explícita da revisão GPT/Gemini: erro não reconhecido não
        // pode virar venda offline silenciosamente — tem que aparecer.
        var causaReal = new NotSupportedException("Alguma coisa nunca vista antes.");
        var embrulhada = new Exception("ERRO NA VENDA (revertido): ...", causaReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeFalse();
    }

    [Fact]
    public void SqlException_SemEmbrulho_AindaEhReconhecidaComoConectividade()
    {
        // Cobre o caminho onde a exceção chega "crua" (sem InnerException) —
        // o classificador precisa lidar com os dois formatos, embrulhado ou não.
        var sqlEx = CriarSqlExceptionFake();

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(sqlEx).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Fase B da migração WPF→API (08/2026) — HttpSaleService pode lançar
    // HttpRequestException/TaskCanceledException, não só SqlException/
    // TimeoutException. Matriz revisada com o GPT antes de mexer no código.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void HttpRequestException_SemEmbrulho_EhConectividade()
    {
        // Caminho real: HttpSaleService lança isso direto (via
        // EnsureSuccessStatusCode ou falha de conexão), sem embrulhar em
        // nada — diferente do caminho local, que sempre embrulha numa
        // Exception genérica dentro da transação do SaleService.
        var httpEx = new HttpRequestException("Servidor inacessível.");

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(httpEx).Should().BeTrue(
            "servidor fora do ar ou rede indisponível tem que cair pro modo offline");
    }

    [Fact]
    public void HttpRequestException_Embrulhada_EhConectividade()
    {
        var httpEx = new HttpRequestException("DNS não resolveu.");
        var embrulhada = new Exception("erro qualquer", httpEx);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(embrulhada).Should().BeTrue();
    }

    [Fact]
    public void TaskCanceledException_ComInnerExceptionTimeoutException_EhConectividade()
    {
        // Comportamento documentado do .NET 5+: quando HttpClient.Timeout
        // estoura, a exceção lançada é TaskCanceledException com
        // InnerException do tipo TimeoutException especificamente — é assim
        // que o .NET diferencia "estourou o tempo" de "alguém cancelou".
        var timeoutReal = new TimeoutException("A operação excedeu o tempo limite configurado.");
        var taskCanceled = new TaskCanceledException("A tarefa foi cancelada.", timeoutReal);

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(taskCanceled).Should().BeTrue(
            "timeout genuíno de HttpClient.Timeout tem que cair pro modo offline, igual TimeoutException do SQL já cai hoje");
    }

    [Fact]
    public void TaskCanceledException_SemInnerException_NaoEhConectividade()
    {
        // Cancelamento intencional via CancellationToken — NÃO tem
        // InnerException nenhum. Não pode ser confundido com timeout de rede.
        var cancelamentoIntencional = new TaskCanceledException("A operação foi cancelada.");

        ConnectivityExceptionClassifier.EhFalhaDeConectividade(cancelamentoIntencional).Should().BeFalse(
            "cancelamento proposital não é queda de conexão — não pode virar venda offline silenciosamente");
    }
}