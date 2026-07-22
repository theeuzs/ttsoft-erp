using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ERP.Persistence.Context;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // S17 FIX: antes tinha uma connection string hardcoded aqui
        // ("Server=localhost\SQLEXPRESS;Database=ERPMateriais;...") sem relação
        // nenhuma com o banco real (ERPTTSoftStaging, no Azure). Isso fazia TODO
        // comando `dotnet ef` (migrations add/list/database update) rodar contra um
        // banco local esquecido, com histórico de migration completamente diferente
        // do banco de verdade — foi exatamente isso que causou a confusão do
        // "Branches não existe" quando o banco real já tinha Branches há meses.
        //
        // Agora lê da mesma fonte que ERP.Api usa em runtime: appsettings.json +
        // appsettings.{Environment}.json + User Secrets. Nunca mais fica desatualizado
        // sozinho -- se a connection string do app mudar, a ferramenta de migration
        // ja enxerga a mudanca automaticamente, sem precisar editar este arquivo de novo.
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ERP.Api");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(basePath) ? basePath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("CONFIGURADO_VIA_AZURE_APP_SERVICE"))
            throw new InvalidOperationException(
                "Nao encontrei uma ConnectionStrings:DefaultConnection valida para rodar a " +
                "migration. Confirme que existe em ERP.Api/appsettings.Development.json ou via " +
                "'dotnet user-secrets set ConnectionStrings:DefaultConnection \"...\" --project ERP.Api'.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            b => b.MigrationsAssembly("ERP.Persistence"));

        // Construtor unico -- sem TenantId (design-time, so para migrations).
        return new AppDbContext(optionsBuilder.Options);
    }
}