using ERP.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface IContaPagarRepository
{
    Task AddAsync(ContaPagar entity);
    Task<IEnumerable<ContaPagar>> GetAllAsync();
    void Update(ContaPagar entity);
    void Remove(ContaPagar entity);
}