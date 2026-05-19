using ERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface IOrcamentoRepository
{
    Task<IEnumerable<Orcamento>> GetAllAsync();
    Task<Orcamento?> GetByIdAsync(Guid id);
    Task AddAsync(Orcamento orcamento);
    void Update(Orcamento orcamento);
}