// ERP.Application/Services/ConnectivityExceptionClassifier.cs
using ERP.Application.Exceptions;
using Microsoft.Data.SqlClient;

namespace ERP.Application.Services;

/// <summary>
/// Fase 2 do Offline-First (docs/OFFLINE_FIRST_ARCHITECTURE.md) — classifica se
/// uma exceção vinda de SaleService.CreateAsync é falha de INFRAESTRUTURA
/// (deveria cair pro modo offline) ou de REGRA DE NEGÓCIO/validação (deveria
/// aparecer pro operador na hora, nunca virar "venda pendente de sincronização").
///
/// Achado de auditoria (08/2026) antes de escrever isso: o catch(Exception)
/// dentro de SaleService.CreateAsync (dentro da transação de estoque+venda)
/// embrulha TUDO — incluindo LimiteCreditoExcedidoException — numa Exception
/// genérica com a causa real em InnerException. Um catch(SqlException) direto
/// em volta da chamada NUNCA dispararia; por isso esse classificador sempre
/// percorre a cadeia de InnerException até a causa raiz, em vez de assumir
/// um número fixo de camadas (achado da revisão cruzada com GPT — a cadeia
/// pode ter mais de um nível: Exception → DbUpdateException → SqlException).
///
/// DbUpdateException é checado pelo NOME do tipo (string), não pelo tipo em
/// si — ERP.Application não referencia o pacote Microsoft.EntityFrameworkCore
/// diretamente (só Microsoft.Data.SqlClient, que outra classe já usava), e é
/// assim de propósito: a camada de Application não deveria depender de
/// detalhe de driver de banco. Confirmado que precisa ser assim ao tentar
/// compilar com o tipo direto — deu erro de referência ausente.
///
/// Isolado numa classe própria (não espalhado pelo FinalizarVendaViewModel)
/// de propósito: o SyncEngineService também vai precisar dessa mesma
/// distinção mais pra frente (retry de infraestrutura é normal; erro de
/// negócio não deveria ficar tentando pra sempre).
/// </summary>
public static class ConnectivityExceptionClassifier
{
    private const string NomeTipoDbUpdateException = "Microsoft.EntityFrameworkCore.DbUpdateException";

    /// <summary>true = falha de infraestrutura/conectividade (rede, SQL Server
    /// inacessível, timeout) — candidata a cair pro modo offline. false =
    /// regra de negócio, validação, ou erro desconhecido — nunca deve virar
    /// venda offline silenciosamente, tem que aparecer pro operador.
    ///
    /// Fail-safe explícito (revisão cruzada com GPT/Gemini): em caso de
    /// dúvida, SEMPRE false. Um PDV nunca deve esconder um erro de negócio
    /// como se fosse queda de conexão.</summary>
    public static bool EhFalhaDeConectividade(Exception ex)
    {
        // Percorre a cadeia inteira de InnerException, não só um nível fixo —
        // a exceção real pode estar duas, três camadas abaixo dependendo de
        // como cada parte do código embrulha (Exception → DbUpdateException →
        // SqlException é um caminho real e confirmado no SaleService hoje;
        // outros pontos do código podem embrulhar diferente no futuro).
        for (var atual = ex; atual != null; atual = atual.InnerException)
        {
            // Regra de negócio conhecida — encontrada em QUALQUER nível da
            // cadeia, decide na hora: NUNCA é conectividade, mesmo que esteja
            // embrulhada dentro de alguma exceção de infraestrutura por fora.
            if (atual is LimiteCreditoExcedidoException) return false;
            if (atual is KeyNotFoundException) return false;
            if (atual is InvalidOperationException) return false;

            // SqlException/TimeoutException em qualquer nível da cadeia — o
            // EnableRetryOnFailure já retentou tudo que é considerado
            // transiente pela própria política do EF Core antes de deixar a
            // exceção escapar até aqui, então se chegou até este ponto é
            // infraestrutura real (ou um erro de schema/query raríssimo em
            // produção estável, que aconteceria igual offline ou online —
            // não é um "falso positivo" perigoso).
            if (atual is SqlException) return true;
            if (atual is TimeoutException) return true;

            // DbUpdateException por si só é ambíguo — só conta como
            // conectividade se algum nível MAIS FUNDO da cadeia (a partir
            // dele) também for SQL/timeout, não uma violação de constraint
            // (dado errado, não rede). Deixa o loop continuar pra baixo — o
            // próprio InnerException do DbUpdateException vai ser checado
            // pela iteração seguinte deste mesmo for.
            if (atual.GetType().FullName == NomeTipoDbUpdateException)
                continue;
        }

        // Nada reconhecido em nenhum nível da cadeia: por segurança, NUNCA
        // vira offline sozinho. Erro desconhecido tem que aparecer pro
        // operador — decisão explícita da revisão GPT/Gemini desse desenho.
        return false;
    }
}