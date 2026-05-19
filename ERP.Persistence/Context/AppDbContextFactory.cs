using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ERP.Persistence.Context;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=ERPMateriais;Trusted_Connection=True;TrustServerCertificate=True;",
            b => b.MigrationsAssembly("ERP.Persistence"));

        // Construtor único — sem TenantId (design-time, só para migrations)
        return new AppDbContext(optionsBuilder.Options);
    }
}