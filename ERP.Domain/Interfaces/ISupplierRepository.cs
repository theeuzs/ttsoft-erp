using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface ISupplierRepository : IRepository<Supplier>
{
    // O IRepository já traz o AddAsync, GetAllAsync, etc de brinde!
}