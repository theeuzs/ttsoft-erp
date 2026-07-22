// ── ERP.Infrastructure/Repositories/ContaBancariaRepository.cs ───────────────
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class ContaBancariaRepository : IContaBancariaRepository
{
    private readonly AppDbContext _context;
    public ContaBancariaRepository(AppDbContext context) => _context = context;

    public async Task<ContaBancaria?> GetByIdAsync(Guid id)
        => await _context.ContasBancarias
            .Include(c => c.Movimentos)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IEnumerable<ContaBancaria>> GetAllAtivasAsync()
        => await _context.ContasBancarias
            .Where(c => c.IsAtiva)
            .OrderBy(c => c.Apelido)
            .ToListAsync();

    public async Task<IEnumerable<ContaBancaria>> GetAllAsync()
        => await _context.ContasBancarias
            .OrderBy(c => c.Apelido)
            .ToListAsync();

    public async Task AddAsync(ContaBancaria conta)
        => await _context.ContasBancarias.AddAsync(conta);

    public void Update(ContaBancaria conta)
        => _context.ContasBancarias.Update(conta);

    public async Task AddMovimentoAsync(MovimentoContaBancaria movimento)
        => await _context.MovimentosContaBancaria.AddAsync(movimento);

    public async Task<IEnumerable<MovimentoContaBancaria>> GetMovimentosAsync(Guid contaBancariaId)
        => await _context.MovimentosContaBancaria
            .AsNoTracking()
            .Where(m => m.ContaBancariaId == contaBancariaId)
            .OrderByDescending(m => m.DataHora)
            .ToListAsync();

    public async Task<decimal> GetSaldoAsync(Guid contaBancariaId)
    {
        var conta = await _context.ContasBancarias.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaBancariaId);
        if (conta is null) return 0m;

        var somaMovimentos = await _context.MovimentosContaBancaria
            .AsNoTracking()
            .Where(m => m.ContaBancariaId == contaBancariaId)
            .SumAsync(m => m.Tipo == TipoMovimentoContaBancaria.Entrada ? m.Valor : -m.Valor);

        return conta.SaldoInicial + somaMovimentos;
    }

    // ── Conciliação Bancária ──────────────────────────────────────────────────
    public async Task<IEnumerable<MovimentoContaBancaria>> BuscarCandidatosConciliacaoAsync(
        Guid contaBancariaId, decimal valor, TipoMovimentoContaBancaria tipo,
        DateTime dataMin, DateTime dataMax)
    {
        return await _context.MovimentosContaBancaria
            .Where(m => m.ContaBancariaId == contaBancariaId
                     && !m.Conciliado
                     && m.Tipo == tipo
                     && m.Valor == valor
                     && m.DataHora.Date >= dataMin.Date
                     && m.DataHora.Date <= dataMax.Date)
            .OrderBy(m => m.DataHora)
            .ToListAsync();
    }

    public async Task<IEnumerable<MovimentoContaBancaria>> GetNaoConciliadosAsync(Guid contaBancariaId)
        => await _context.MovimentosContaBancaria
            .AsNoTracking()
            .Where(m => m.ContaBancariaId == contaBancariaId && !m.Conciliado)
            .OrderByDescending(m => m.DataHora)
            .ToListAsync();

    public async Task MarcarConciliadoAsync(Guid movimentoId, string? fitId)
        => await _context.MovimentosContaBancaria
            .Where(m => m.Id == movimentoId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Conciliado, true)
                .SetProperty(m => m.FitId, fitId));

    public async Task<bool> FitIdJaProcessadoAsync(Guid contaBancariaId, string fitId)
        => await _context.MovimentosContaBancaria
            .AsNoTracking()
            .AnyAsync(m => m.ContaBancariaId == contaBancariaId && m.FitId == fitId);

    public async Task<ContaBancaria?> GetContaPadraoAsync()
        => await _context.ContasBancarias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ContaPadrao && c.IsAtiva);

    public async Task DefinirContaPadraoAsync(Guid contaBancariaId)
    {
        // Desmarca qualquer conta que já fosse padrão antes de marcar a nova —
        // só uma conta padrão por vez.
        await _context.ContasBancarias
            .Where(c => c.ContaPadrao && c.Id != contaBancariaId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ContaPadrao, false));

        await _context.ContasBancarias
            .Where(c => c.Id == contaBancariaId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ContaPadrao, true));
    }

    public async Task<IEnumerable<MovimentoContaBancaria>> GetMovimentosPorPeriodoAsync(DateTime inicio, DateTime fim)
        => await _context.MovimentosContaBancaria
            .AsNoTracking()
            .Include(m => m.ContaBancaria)
            .Where(m => m.DataHora >= inicio && m.DataHora <= fim)
            .OrderByDescending(m => m.DataHora)
            .ToListAsync();
}