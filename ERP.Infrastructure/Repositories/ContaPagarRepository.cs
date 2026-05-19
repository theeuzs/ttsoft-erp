using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Infrastructure.Repositories;

public class ContaPagarRepository : IContaPagarRepository
{
    private readonly AppDbContext _ctx;

    public ContaPagarRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task AddAsync(ContaPagar entity) => await _ctx.ContasPagar.AddAsync(entity);
    
    public async Task<IEnumerable<ContaPagar>> GetAllAsync() => await _ctx.ContasPagar.ToListAsync();
    
    public void Update(ContaPagar entity) => _ctx.ContasPagar.Update(entity);
    
    public void Remove(ContaPagar entity) => _ctx.ContasPagar.Remove(entity);
}