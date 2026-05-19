using ERP.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface IContaReceberRepository
{
    Task AddAsync(ContaReceber entity);
    Task<IEnumerable<ContaReceber>> GetAllAsync();
    Task<IEnumerable<ContaReceber>> GetBySaleIdAsync(Guid saleId);
    void Update(ContaReceber entity);
}