using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ERP.Api.Services;

/// <summary>
/// S13: Remove registros de cadastro pendentes cujo ConfirmacaoToken expirou (48h).
/// Sem este cleanup, atacante que pré-registra um CNPJ com e-mail divergente bloqueia
/// o dono real para sempre (DoS via pré-registro).
///
/// Executa a cada 6 horas. Remove: admin inativo + roles + permissions do tenant.
/// Fail-safe: exceções por tenant individual são logadas e puladas — evita que um
/// erro de banco em um tenant interrompa o cleanup dos demais.
/// </summary>
public class CleanupCadastrosExpiradosService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    public CleanupCadastrosExpiradosService(IServiceProvider sp) => _sp = sp;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Aguarda 5 minutos após o startup para não concorrer com migrations
        await Task.Delay(TimeSpan.FromMinutes(5), ct);

        while (!ct.IsCancellationRequested)
        {
            await LimparExpiradosAsync(ct);
            await Task.Delay(Intervalo, ct);
        }
    }

    private async Task LimparExpiradosAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var agora = DateTime.UtcNow;

            // Admins inativos com token expirado
            var expirados = await db.Users
                .IgnoreQueryFilters()
                .Where(u => !u.IsActive
                         && u.ConfirmacaoToken != null
                         && u.ConfirmacaoTokenExpiraEm != null
                         && u.ConfirmacaoTokenExpiraEm < agora)
                .ToListAsync(ct);

            if (expirados.Count == 0) return;

            var tenantIds = expirados.Select(u => u.TenantId).Distinct().ToList();

            foreach (var tenantId in tenantIds)
            {
                try
                {
                    // Remove permissions → roles → user do tenant pendente
                    var perms = db.Permissions
                        .IgnoreQueryFilters()
                        .Where(p => p.TenantId == tenantId);
                    db.Permissions.RemoveRange(perms);

                    var roles = db.Roles
                        .IgnoreQueryFilters()
                        .Where(r => r.TenantId == tenantId);
                    db.Roles.RemoveRange(roles);

                    var users = db.Users
                        .IgnoreQueryFilters()
                        .Where(u => u.TenantId == tenantId && !u.IsActive && u.ConfirmacaoToken != null);
                    db.Users.RemoveRange(users);

                    await db.SaveChangesAsync(ct);

                    Log.Information("Cleanup: tenant pendente expirado removido — TenantId={TenantId}", tenantId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Cleanup: erro ao remover tenant expirado TenantId={TenantId} — pulado", tenantId);
                }
            }

            Log.Information("Cleanup: {N} cadastros expirados removidos", expirados.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cleanup: erro geral no CleanupCadastrosExpiradosService");
        }
    }
}
