using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
// Adicione o using do seu AppDbContext aqui (depende de onde ele fica na sua Infra)

namespace ERP.Infrastructure.Repositories; // 👈 Olha o namespace da sua Infra aqui!

public class SupplierRepository : Repository<Supplier>, ISupplierRepository
{
    public SupplierRepository(AppDbContext context) : base(context)
    {
    }
}