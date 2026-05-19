using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class EntregaService : IEntregaService
{
    private readonly AppDbContext _ctx;
    public EntregaService(AppDbContext ctx) => _ctx = ctx;

    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<EntregaDto>> GetAllAsync(
        DateTime? data = null, string? status = null, Guid? motoristaId = null)
    {
        var query = _ctx.Set<Entrega>().AsNoTracking()
            .Include(e => e.Veiculo)
            .Where(e => !e.IsDeleted);

        if (data.HasValue)
            query = query.Where(e => e.DataPrevista.Date == data.Value.Date);

        if (!string.IsNullOrEmpty(status) &&
            Enum.TryParse<StatusEntrega>(status, true, out var st))
            query = query.Where(e => e.Status == st);

        if (motoristaId.HasValue)
            query = query.Where(e => e.MotoristaId == motoristaId);

        var lista = await query
            .OrderBy(e => e.DataPrevista)
            .ThenBy(e => e.Status)
            .ToListAsync();

        return lista.Select(Map);
    }

    public async Task<EntregaDto?> GetByIdAsync(Guid id)
    {
        var e = await _ctx.Set<Entrega>().AsNoTracking()
            .Include(e => e.Veiculo)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        return e is null ? null : Map(e);
    }

    public async Task<EntregaDto?> GetBySaleAsync(Guid saleId)
    {
        var e = await _ctx.Set<Entrega>().AsNoTracking()
            .Include(e => e.Veiculo)
            .FirstOrDefaultAsync(e => e.SaleId == saleId && !e.IsDeleted);
        return e is null ? null : Map(e);
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<EntregaDto> CreateAsync(CreateEntregaDto dto)
    {
        // Resolve nome do motorista se informado
        string? motoNome = null;
        if (dto.MotoristaId.HasValue)
        {
            motoNome = await _ctx.Users.AsNoTracking()
                .Where(u => u.Id == dto.MotoristaId.Value)
                .Select(u => u.Name)
                .FirstOrDefaultAsync();
        }

        var entrega = new Entrega
        {
            SaleId        = dto.SaleId,
            CustomerId    = dto.CustomerId,
            ClienteNome   = dto.ClienteNome,
            Logradouro    = dto.Logradouro,
            Numero        = dto.Numero,
            Complemento   = dto.Complemento,
            Bairro        = dto.Bairro,
            Cidade        = dto.Cidade,
            UF            = dto.UF,
            CEP           = dto.CEP,
            Referencia    = dto.Referencia,
            DataPrevista  = dto.DataPrevista,
            JanelaHorario = dto.JanelaHorario,
            MotoristaId   = dto.MotoristaId,
            MotoristaNome = motoNome,
            VeiculoId     = dto.VeiculoId,
            Observacoes   = dto.Observacoes,
            CustoEntrega  = dto.CustoEntrega,
            Status        = StatusEntrega.Pendente
        };

        _ctx.Set<Entrega>().Add(entrega);
        await _ctx.SaveChangesAsync();

        return await GetByIdAsync(entrega.Id) ?? Map(entrega);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entrega = await _ctx.Set<Entrega>()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (entrega is null) return;

        entrega.IsDeleted = true;
        entrega.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    // ── Operação ─────────────────────────────────────────────────────────────

    public async Task<EntregaDto> AtualizarStatusAsync(Guid id, AtualizarStatusEntregaDto dto)
    {
        var entrega = await _ctx.Set<Entrega>()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted)
            ?? throw new KeyNotFoundException($"Entrega {id} não encontrada.");

        entrega.Status        = dto.Status;
        entrega.UpdatedAt     = DateTime.UtcNow;

        if (dto.Status == StatusEntrega.Entregue)
        {
            entrega.DataEntrega     = DateTime.Now;
            entrega.AssinadoPor     = dto.AssinadoPor;
            entrega.FotoComprovante = dto.FotoComprovante;
        }
        else if (dto.Status == StatusEntrega.Cancelada ||
                 dto.Status == StatusEntrega.Reagendada)
        {
            entrega.MotivoProblema = dto.MotivoProblema;
            if (dto.Status == StatusEntrega.Reagendada && dto.NovaDataPrevista.HasValue)
            {
                entrega.DataPrevista = dto.NovaDataPrevista.Value;
                entrega.Status       = StatusEntrega.Pendente; // volta para Pendente na nova data
            }
        }

        await _ctx.SaveChangesAsync();
        return Map(entrega);
    }

    public async Task<EntregaDto> AtribuirMotoristaAsync(Guid id, AtribuirMotoristaDto dto)
    {
        var entrega = await _ctx.Set<Entrega>()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted)
            ?? throw new KeyNotFoundException($"Entrega {id} não encontrada.");

        var motoristaNome = await _ctx.Users.AsNoTracking()
            .Where(u => u.Id == dto.MotoristaId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync()
            ?? "Motorista";

        entrega.MotoristaId   = dto.MotoristaId;
        entrega.MotoristaNome = motoristaNome;
        entrega.VeiculoId     = dto.VeiculoId;
        entrega.UpdatedAt     = DateTime.UtcNow;

        // Quando atribui motorista, muda para EmRota automaticamente
        if (entrega.Status == StatusEntrega.Pendente)
            entrega.Status = StatusEntrega.EmRota;

        await _ctx.SaveChangesAsync();
        return Map(entrega);
    }

    // ── Relatórios ────────────────────────────────────────────────────────────

    public async Task<RelatorioEntregasDto> GetRelatorioAsync(DateTime data)
    {
        var entregas = await _ctx.Set<Entrega>().AsNoTracking()
            .Where(e => !e.IsDeleted && e.DataPrevista.Date == data.Date)
            .ToListAsync();

        var porMotorista = entregas
            .Where(e => !string.IsNullOrEmpty(e.MotoristaNome))
            .GroupBy(e => e.MotoristaNome!)
            .Select(g => new RelatorioMotoristaDto
            {
                MotoristaNome = g.Key,
                TotalEntregas = g.Count(),
                Entregues     = g.Count(e => e.Status == StatusEntrega.Entregue),
                Pendentes     = g.Count(e => e.Status == StatusEntrega.Pendente ||
                                             e.Status == StatusEntrega.EmRota),
                CustoTotal    = g.Sum(e => e.CustoEntrega)
            }).ToList();

        return new RelatorioEntregasDto
        {
            TotalDia      = entregas.Count,
            Entregues     = entregas.Count(e => e.Status == StatusEntrega.Entregue),
            Pendentes     = entregas.Count(e => e.Status == StatusEntrega.Pendente),
            EmRota        = entregas.Count(e => e.Status == StatusEntrega.EmRota),
            Canceladas    = entregas.Count(e => e.Status == StatusEntrega.Cancelada),
            CustoTotal    = entregas.Sum(e => e.CustoEntrega),
            PorMotorista  = porMotorista
        };
    }

    // ── Veículos ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<VeiculoDto>> GetVeiculosAsync()
    {
        return await _ctx.Set<Veiculo>().AsNoTracking()
            .Where(v => !v.IsDeleted && v.IsAtivo)
            .OrderBy(v => v.Placa)
            .Select(v => new VeiculoDto
            {
                Id        = v.Id,
                Placa     = v.Placa,
                Tipo      = v.Tipo,
                Modelo    = v.Modelo,
                Capacidade = v.Capacidade,
                IsAtivo   = v.IsAtivo
            }).ToListAsync();
    }

    public async Task<VeiculoDto> CreateVeiculoAsync(CreateVeiculoDto dto)
    {
        var v = new Veiculo
        {
            Placa      = dto.Placa.ToUpper().Trim(),
            Tipo       = dto.Tipo,
            Modelo     = dto.Modelo,
            Capacidade = dto.Capacidade,
            IsAtivo    = true
        };
        _ctx.Set<Veiculo>().Add(v);
        await _ctx.SaveChangesAsync();
        return new VeiculoDto { Id = v.Id, Placa = v.Placa, Tipo = v.Tipo,
                                 Modelo = v.Modelo, Capacidade = v.Capacidade, IsAtivo = v.IsAtivo };
    }

    public async Task DeleteVeiculoAsync(Guid id)
    {
        var v = await _ctx.Set<Veiculo>().FirstOrDefaultAsync(v => v.Id == id);
        if (v is null) return;
        v.IsDeleted = true;
        v.IsAtivo   = false;
        v.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    // ── Mapeamento ────────────────────────────────────────────────────────────

    private static EntregaDto Map(Entrega e) => new()
    {
        Id              = e.Id,
        SaleId          = e.SaleId,
        ClienteNome     = e.ClienteNome,
        Logradouro      = e.Logradouro,
        Numero          = e.Numero,
        Complemento     = e.Complemento,
        Bairro          = e.Bairro,
        Cidade          = e.Cidade,
        UF              = e.UF,
        CEP             = e.CEP,
        Referencia      = e.Referencia,
        DataPrevista    = e.DataPrevista,
        DataEntrega     = e.DataEntrega,
        JanelaHorario   = e.JanelaHorario,
        Status          = e.Status,
        MotoristaId     = e.MotoristaId,
        MotoristaNome   = e.MotoristaNome,
        VeiculoId       = e.VeiculoId,
        VeiculoPlaca    = e.Veiculo?.Placa,
        Observacoes     = e.Observacoes,
        MotivoProblema  = e.MotivoProblema,
        AssinadoPor     = e.AssinadoPor,
        CustoEntrega    = e.CustoEntrega,
        CreatedAt       = e.CreatedAt
    };
}
