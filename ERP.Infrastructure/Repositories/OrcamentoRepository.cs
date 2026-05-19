using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Infrastructure.Repositories;

public class OrcamentoRepository : IOrcamentoRepository
{
    private readonly AppDbContext _context;

    public OrcamentoRepository(AppDbContext context) { _context = context; }

    public async Task<IEnumerable<Orcamento>> GetAllAsync()
    {
        // Traz os orçamentos com os itens embutidos, ordenando pelos mais recentes
        return await _context.Orcamentos
            .Include(o => o.Itens)
            .OrderByDescending(o => o.DataEmissao)
            .ToListAsync();
    }

    public async Task<Orcamento?> GetByIdAsync(Guid id)
    {
        return await _context.Orcamentos
            .Include(o => o.Itens)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task AddAsync(Orcamento orcamento)
    {
        await _context.Orcamentos.AddAsync(orcamento);
    }

    public void Update(Orcamento orcamento)
    {
        _context.Orcamentos.Update(orcamento);
    }
}