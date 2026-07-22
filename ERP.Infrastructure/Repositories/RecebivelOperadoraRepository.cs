// ── ERP.Infrastructure/Repositories/RecebivelOperadoraRepository.cs ──────────
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class RecebivelOperadoraRepository : IRecebivelOperadoraRepository
{
    private readonly AppDbContext _context;
    public RecebivelOperadoraRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(RecebivelOperadora recebivel)
        => await _context.RecebiveisOperadora.AddAsync(recebivel);

    public async Task<IEnumerable<RecebivelOperadora>> GetPendentesAsync()
        => await _context.RecebiveisOperadora
            .AsNoTracking()
            .Include(r => r.OperadoraRecebimento)
            .Where(r => r.Status == StatusRecebivel.Pendente)
            .OrderByDescending(r => r.DataVenda)
            .ToListAsync();

    public async Task<IEnumerable<RecebivelOperadora>> GetByIdsAsync(IEnumerable<Guid> ids)
        => await _context.RecebiveisOperadora
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .ToListAsync();

    public async Task MarcarLiquidadosAsync(IEnumerable<Guid> ids, Guid movimentoContaBancariaId, DateTime dataLiquidacao)
        => await _context.RecebiveisOperadora
            .Where(r => ids.Contains(r.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, StatusRecebivel.Liquidado)
                .SetProperty(r => r.DataLiquidacao, dataLiquidacao)
                .SetProperty(r => r.MovimentoContaBancariaId, movimentoContaBancariaId));

    public async Task<IEnumerable<RecebivelOperadora>> GetPorPeriodoAsync(DateTime inicio, DateTime fim)
        => await _context.RecebiveisOperadora
            .AsNoTracking()
            .Include(r => r.OperadoraRecebimento)
            .Where(r => r.DataVenda >= inicio && r.DataVenda <= fim)
            .OrderByDescending(r => r.DataVenda)
            .ToListAsync();

    public async Task<IEnumerable<RecebivelOperadora>> GetByVendaIdAsync(Guid vendaId)
        => await _context.RecebiveisOperadora
            .AsNoTracking()
            .Where(r => r.VendaId == vendaId)
            .ToListAsync();

    public async Task CancelarPendentesPorVendaAsync(Guid vendaId)
        => await _context.RecebiveisOperadora
            .Where(r => r.VendaId == vendaId && r.Status == StatusRecebivel.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, StatusRecebivel.Cancelado));
}