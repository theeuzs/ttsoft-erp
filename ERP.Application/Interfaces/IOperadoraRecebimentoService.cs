// ── ERP.Application/Interfaces/IOperadoraRecebimentoService.cs ───────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IOperadoraRecebimentoService
{
    Task<IReadOnlyList<OperadoraRecebimentoDto>> ObterAtivasAsync();
    Task CriarAsync(CriarOperadoraRecebimentoDto dto);
    Task InativarAsync(Guid id);

    /// <summary>Marca uma operadora como padrão pro PDV, desmarcando qualquer outra.</summary>
    Task DefinirComoPadraoAsync(Guid id);
}