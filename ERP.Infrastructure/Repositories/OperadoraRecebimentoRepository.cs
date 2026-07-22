// ── ERP.Infrastructure/Repositories/OperadoraRecebimentoRepository.cs ────────
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class OperadoraRecebimentoRepository : IOperadoraRecebimentoRepository
{
    private readonly AppDbContext _context;
    public OperadoraRecebimentoRepository(AppDbContext context) => _context = context;

    public async Task<OperadoraRecebimento?> GetByIdAsync(Guid id)
        => await _context.OperadorasRecebimento
            .Include(o => o.ContaDestino)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IEnumerable<OperadoraRecebimento>> GetAllAtivasAsync()
        => await _context.OperadorasRecebimento
            .Include(o => o.ContaDestino)
            .Where(o => o.IsAtiva)
            .OrderBy(o => o.Nome)
            .ToListAsync();

    public async Task AddAsync(OperadoraRecebimento operadora)
        => await _context.OperadorasRecebimento.AddAsync(operadora);

    public void Update(OperadoraRecebimento operadora)
        => _context.OperadorasRecebimento.Update(operadora);

    public async Task<OperadoraRecebimento?> GetPadraoAsync()
        => await _context.OperadorasRecebimento
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OperadoraPadrao && o.IsAtiva);

    public async Task DefinirPadraoAsync(Guid id)
    {
        await _context.OperadorasRecebimento
            .Where(o => o.OperadoraPadrao && o.Id != id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OperadoraPadrao, false));

        await _context.OperadorasRecebimento
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OperadoraPadrao, true));
    }
}