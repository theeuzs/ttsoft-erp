// ── ERP.Infrastructure/Repositories/VendaSuspensaRepository.cs ───────────────
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class VendaSuspensaRepository : IVendaSuspensaRepository
{
    private readonly AppDbContext _context;
    public VendaSuspensaRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(VendaSuspensa venda)
        => await _context.VendasSuspensas.AddAsync(venda);

    public async Task<VendaSuspensa?> GetByIdAsync(Guid id)
        => await _context.VendasSuspensas
            .AsNoTracking()
            .Include(v => v.Itens)
            .FirstOrDefaultAsync(v => v.Id == id);

    public async Task<IEnumerable<VendaSuspensa>> GetPendentesAsync()
        => await _context.VendasSuspensas
            .AsNoTracking()
            .Include(v => v.Itens)
            .Where(v => v.Status == StatusVendaSuspensa.Suspensa)
            .OrderBy(v => v.DataSuspensao)
            .ToListAsync();

    public async Task RemoverItensAsync(Guid vendaSuspensaId)
        => await _context.VendaSuspensaItens
            .Where(i => i.VendaSuspensaId == vendaSuspensaId)
            .ExecuteDeleteAsync();

    public async Task AdicionarItensAsync(IEnumerable<VendaSuspensaItem> itens)
        => await _context.VendaSuspensaItens.AddRangeAsync(itens);

    public async Task IniciarEdicaoAsync(Guid id, Guid usuarioId, string nomeUsuario, DateTime dataInicio)
        => await _context.VendasSuspensas
            .Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.UsuarioIdEmEdicao, usuarioId)
                .SetProperty(v => v.NomeEmEdicao, nomeUsuario)
                .SetProperty(v => v.DataInicioEdicao, dataInicio));

    public async Task LiberarEdicaoAsync(Guid id)
        => await _context.VendasSuspensas
            .Where(v => v.Id == id && v.Status == StatusVendaSuspensa.Suspensa)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.UsuarioIdEmEdicao, (Guid?)null)
                .SetProperty(v => v.NomeEmEdicao, (string?)null)
                .SetProperty(v => v.DataInicioEdicao, (DateTime?)null));

    public async Task AtualizarCabecalhoESuspenderAsync(Guid id, Guid? clienteId, string clienteNome, decimal totalAproximado)
        => await _context.VendasSuspensas
            .Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.ClienteId, clienteId)
                .SetProperty(v => v.ClienteNome, clienteNome)
                .SetProperty(v => v.TotalAproximado, totalAproximado)
                .SetProperty(v => v.Status, StatusVendaSuspensa.Suspensa)
                .SetProperty(v => v.UsuarioIdEmEdicao, (Guid?)null)
                .SetProperty(v => v.NomeEmEdicao, (string?)null)
                .SetProperty(v => v.DataInicioEdicao, (DateTime?)null));

    public async Task FinalizarAsync(Guid id, Guid vendaId)
        => await _context.VendasSuspensas
            .Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, StatusVendaSuspensa.Finalizada)
                .SetProperty(v => v.VendaFinalizadaId, vendaId)
                .SetProperty(v => v.UsuarioIdEmEdicao, (Guid?)null)
                .SetProperty(v => v.NomeEmEdicao, (string?)null)
                .SetProperty(v => v.DataInicioEdicao, (DateTime?)null));

    public async Task DescartarAsync(Guid id)
        => await _context.VendasSuspensas
            .Where(v => v.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.Status, StatusVendaSuspensa.Descartada)
                .SetProperty(v => v.UsuarioIdEmEdicao, (Guid?)null)
                .SetProperty(v => v.NomeEmEdicao, (string?)null)
                .SetProperty(v => v.DataInicioEdicao, (DateTime?)null));
}