// ERP.Tests/Application/ConnectivityExceptionClassifierTests.cs
using ERP.Application.Exceptions;
using ERP.Application.Services;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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
}