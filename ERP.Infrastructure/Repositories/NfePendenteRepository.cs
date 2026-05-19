using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Infrastructure.Repositories;

public class NfePendenteRepository : INfePendenteRepository
{
    private readonly AppDbContext _context;

    public NfePendenteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<NfePendente>> GetAllAsync()
    {
        return await _context.Set<NfePendente>().ToListAsync();
    }

    public async Task<NfePendente?> GetByIdAsync(Guid id)
    {
        return await _context.Set<NfePendente>().FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(NfePendente entity)
    {
        await _context.Set<NfePendente>().AddAsync(entity);
    }

    public void Update(NfePendente entity)
    {
        _context.Set<NfePendente>().Update(entity);
    }

    public void Remove(NfePendente entity)
    {
        _context.Set<NfePendente>().Remove(entity);
    }
}