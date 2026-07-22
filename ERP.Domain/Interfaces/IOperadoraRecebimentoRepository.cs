// ── ERP.Domain/Interfaces/IOperadoraRecebimentoRepository.cs ─────────────────
using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface IOperadoraRecebimentoRepository
{
    Task<OperadoraRecebimento?> GetByIdAsync(Guid id);
    Task<IEnumerable<OperadoraRecebimento>> GetAllAtivasAsync();
    Task AddAsync(OperadoraRecebimento operadora);
    void Update(OperadoraRecebimento operadora);

    /// <summary>A operadora marcada como padrão pro PDV — null se nenhuma configurada.</summary>
    Task<OperadoraRecebimento?> GetPadraoAsync();

    /// <summary>Marca uma operadora como padrão, desmarcando qualquer outra que já fosse.</summary>
    Task DefinirPadraoAsync(Guid id);
}