// ── ERP.Application/Interfaces/IConciliacaoService.cs ────────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de conciliação bancária/cartão — recebe o CSV do extrato da
/// operadora e cruza com as vendas do período. Encapsula toda a lógica de
/// parsing, sanitização e acesso a dados — controllers não tocam em
/// AppDbContext diretamente.
/// </summary>
public interface IConciliacaoService
{
    /// <summary>
    /// Importa e concilia um extrato CSV. Lança InvalidOperationException com
    /// mensagem amigável para qualquer problema de validação do arquivo
    /// (CSV vazio, colunas faltando, muito grande, nenhuma transação válida) —
    /// controller mapeia para 400.
    /// </summary>
    Task<ConciliacaoResultadoDto> ImportarExtratoAsync(
        Stream csvStream, string? separador, CancellationToken ct = default);
}
