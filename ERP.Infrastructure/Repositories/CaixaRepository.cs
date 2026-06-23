using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using ERP.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic; // 🟢 Necessário para a nossa lista
using System.Threading.Tasks;

namespace ERP.Infrastructure.Repositories;

public class CaixaRepository : ICaixaRepository
{
    private readonly AppDbContext _context; 

    public CaixaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Caixa?> GetCaixaAbertoAsync()
    {
        return await _context.Caixas
            .Include(c => c.Movimentos) 
            .FirstOrDefaultAsync(c => c.Status == ERP.Domain.Enums.StatusCaixa.Aberto);
    }

    public async Task<Caixa?> GetCaixaAbertoByUsuarioAsync(Guid usuarioId)
    {
        return await _context.Caixas
            .Include(c => c.Movimentos)
            .FirstOrDefaultAsync(c => c.Status == StatusCaixa.Aberto 
                                   && c.UsuarioId == usuarioId);
    }

    public async Task<Caixa?> GetByIdAsync(Guid id)
    {
        return await _context.Caixas
            .Include(c => c.Movimentos)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    // 👇 A MÁGICA DA MÁQUINA DO TEMPO ENTRA AQUI 👇
    public async Task<IEnumerable<Caixa>> GetAllAsync()
    {
        // Retorna todos os caixas e suas movimentações financeiras associadas
        return await _context.Caixas
            .Include(c => c.Movimentos)
            .ToListAsync();
    }

    public async Task AddAsync(Caixa caixa)
    {
        await _context.Caixas.AddAsync(caixa);
    }

    public void Update(Caixa caixa)
    {
        _context.Caixas.Update(caixa);
    }

    public async Task AddMovimentoAsync(CaixaMovimento movimento)
    {
        await _context.AddAsync(movimento);
    }

    // S8: computa saldo em dinheiro para validação de sangria.
    // Abertura + Suprimento + vendas em dinheiro - Sangrias.
    // Movimentos de venda sem FormaPagamento explícita contam como dinheiro (legado).
    public async Task<decimal> GetSaldoDinheiroAsync(Guid caixaId)
    {
        return await _context.CaixaMovimentos
            .AsNoTracking()
            .Where(m => m.CaixaId == caixaId)
            .SumAsync(m =>
                m.Tipo == TipoMovimentoCaixa.Sangria    ? -m.Valor :
                m.Tipo == TipoMovimentoCaixa.Suprimento ?  m.Valor :
                m.Tipo == TipoMovimentoCaixa.Abertura   ?  m.Valor :
                (m.FormaPagamento == ERP.Domain.Enums.PaymentMethod.Dinheiro
                 || m.FormaPagamento == null)            ?  m.Valor : 0m);
    }
}