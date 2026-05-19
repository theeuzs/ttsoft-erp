using ERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface INfePendenteRepository
{
    Task<IEnumerable<NfePendente>> GetAllAsync();
    Task<NfePendente?> GetByIdAsync(Guid id);
    Task AddAsync(NfePendente entity);
    void Update(NfePendente entity);
    void Remove(NfePendente entity);
}