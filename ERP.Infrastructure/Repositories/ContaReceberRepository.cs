using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Infrastructure.Repositories;

public class ContaReceberRepository : IContaReceberRepository
{
    private readonly AppDbContext _ctx;

    public ContaReceberRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task AddAsync(ContaReceber entity)
    {
        // Pega a tabela que criamos no SQL e adiciona a dívida
        await _ctx.ContasReceber.AddAsync(entity);
    }
    public async Task<IEnumerable<ContaReceber>> GetAllAsync()
    {
        return await _ctx.ContasReceber
            .Include(c => c.Customer)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContaReceber>> GetBySaleIdAsync(Guid saleId)
    {
        return await _ctx.ContasReceber
            .AsNoTracking()
            .Where(c => c.SaleId == saleId)
            .ToListAsync();
    }

    public void Update(ContaReceber entity)
        => _ctx.ContasReceber.Update(entity);
}