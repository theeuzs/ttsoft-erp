namespace ERP.Application.Exceptions;

/// <summary>
/// Fase B da migração WPF→API (08/2026) — a API respondeu 401 (não
/// autorizado): o token JWT está vazio, expirado ou inválido. Tipo dedicado
/// (não HttpRequestException genérica) por duas razões:
///
/// 1. ConnectivityExceptionClassifier nunca deveria classificar isso como
///    falha de rede — a internet pode estar perfeitamente funcionando, só
///    falta uma credencial válida. Não precisou de nenhuma mudança no
///    classificador: ele já trata tipo desconhecido como "nunca offline"
///    (fail-safe existente), e essa exceção simplesmente nunca bate em
///    nenhum `if` dele, então cai corretamente nesse fail-safe.
///
/// 2. AppSession.JwtToken é obtido uma única vez, no login, best-effort
///    (se a API estava fora do ar naquele momento, fica vazio pro turno
///    inteiro) — e o próprio JWT expira em 8h (Jwt:ExpirationHours),
///    quase a duração de um turno de loja. Os dois casos (nunca teve
///    token / token venceu) se manifestam da mesma forma (401) e têm a
///    mesma correção possível hoje: pedir pro operador logar de novo —
///    não existe endpoint de refresh na API.
///
/// De propósito SEM mensagem de UI embutida aqui — a mensagem é técnica,
/// neutra, sem acoplar a apresentação; quem decide como mostrar isso pro
/// operador é a camada de UI (ViewModel), não essa exceção.
/// </summary>
public class SessaoExpiradaException : Exception
{
    public SessaoExpiradaException()
        : base("Sessão da API expirada ou inválida (HTTP 401).") { }

    public SessaoExpiradaException(string message) : base(message) { }
}
