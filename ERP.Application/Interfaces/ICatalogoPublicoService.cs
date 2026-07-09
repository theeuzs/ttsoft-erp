// ── ERP.Application/Interfaces/ICatalogoPublicoService.cs ────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço do catálogo público de produtos (endpoint AllowAnonymous).
/// ATENÇÃO — lógica de segurança sensível, já teve 2 correções documentadas:
///   S10: filtros aplicados no banco antes de paginar (TotalItems correto).
///   S11: opt-in obrigatório (CatalogoPublicoHabilitado) antes de expor
///        catálogo de qualquer tenant achável por CNPJ — sem isso, qualquer
///        concorrente conseguia raspar preço/estoque/portfólio.
/// Qualquer alteração aqui precisa preservar exatamente essas duas garantias.
/// </summary>
public interface ICatalogoPublicoService
{
    /// <summary>
    /// Retorna null quando um tenantId explícito é informado mas a loja não
    /// habilitou o catálogo público — controller mapeia para 404 SEM revelar
    /// se o tenant existe ou não (mesma resposta para "não existe" e "existe
    /// mas não habilitou").
    /// </summary>
    Task<CatalogoResultadoDto?> GetCatalogoAsync(
        int page, int pageSize, string? search, string? categoria, Guid? tenantId,
        CancellationToken ct = default);
}
