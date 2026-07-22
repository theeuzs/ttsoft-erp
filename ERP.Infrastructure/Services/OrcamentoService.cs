using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Infrastructure.Services;

public class OrcamentoService : IOrcamentoService
{
    private readonly IUnitOfWork  _uow;
    private readonly AppDbContext _ctx;

    public OrcamentoService(IUnitOfWork uow, AppDbContext ctx)
    {
        _uow = uow;
        _ctx = ctx;
    }

    public async Task<Orcamento> SalvarOrcamentoAsync(CreateOrcamentoDto dto)
    {
        var orcamento = new Orcamento
        {
            Id = Guid.NewGuid(),
            Numero = $"ORC{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(10, 99)}", 
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            SellerName = dto.SellerName, 
            
            // 👇 1. Vinculando o Orçamento ao ID do usuário logado!
            UsuarioId = dto.UsuarioId,

            // S17: campos novos — antes não existiam na tela nenhuma
            Observacao = dto.Observacao
            
            // ❌ Tiramos o ValorTotal daqui! O backend vai calcular sozinho.
        };

        if (dto.AgendarFollowUp && dto.DataFollowUp.HasValue)
        {
            orcamento.DataFollowUp = dto.DataFollowUp;
            orcamento.StatusFollowUp = StatusFollowUp.Pendente;
        }

        foreach (var item in dto.Itens)
        {
            orcamento.Itens.Add(new OrcamentoItem
            {
                Id = Guid.NewGuid(),
                OrcamentoId = orcamento.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent
            });
        }

        // 👇 2. O BACKEND NO CONTROLE: Calcula o total blindado contra erros da tela!
        orcamento.RecalcularTotal();

        // S17: RecalcularTotal() sempre setava 7 dias fixo — sobrescreve se o
        // usuário escolheu outra validade na tela.
        if (dto.ValidadeDias > 0)
            orcamento.DataValidade = DateTime.Now.AddDays(dto.ValidadeDias);

        await _uow.Orcamentos.AddAsync(orcamento);
        await _uow.CommitAsync();

        return orcamento;
    }

    public async Task<IEnumerable<Orcamento>> ObterTodosAsync()
    {
        return await _uow.Orcamentos.GetAllAsync();
    }

    public async Task MarcarComoVendidoAsync(Guid id)
    {
        var orc = await _uow.Orcamentos.GetByIdAsync(id);
        if (orc != null)
        {
            orc.Status       = StatusOrcamento.Vendido;
            orc.StatusFollowUp = StatusFollowUp.Convertido;
            _uow.Orcamentos.Update(orc);
            await _uow.CommitAsync();
        }
    }

    // ── CRM / Follow-Up ──────────────────────────────────────────────────────

    public async Task<IEnumerable<OrcamentoFollowUpDto>> GetAgendaHojeAsync()
    {
        var hoje = DateTime.Today;
        var orcamentos = await _ctx.Orcamentos.AsNoTracking()
            .Where(o => !o.IsDeleted &&
                       o.Status == StatusOrcamento.Aberto &&
                       o.DataFollowUp.HasValue &&
                       o.DataFollowUp.Value.Date <= hoje &&       // hoje ou vencidos
                       o.StatusFollowUp == StatusFollowUp.Pendente)
            .OrderBy(o => o.DataFollowUp)
            .ToListAsync();

        return orcamentos.Select(MapToFollowUpDto);
    }

    public async Task<IEnumerable<OrcamentoFollowUpDto>> GetTodosComFollowUpAsync()
    {
        var orcamentos = await _ctx.Orcamentos.AsNoTracking()
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.DataEmissao)
            .ToListAsync();

        return orcamentos.Select(MapToFollowUpDto);
    }

    public async Task AgendarFollowUpAsync(Guid id, AgendarFollowUpDto dto)
    {
        var orc = await _ctx.Orcamentos.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
            ?? throw new KeyNotFoundException($"Orçamento {id} não encontrado.");

        orc.DataFollowUp       = dto.DataFollowUp;
        orc.StatusFollowUp     = StatusFollowUp.Pendente;
        orc.ObservacaoFollowUp = dto.Observacao;
        orc.UpdatedAt          = DateTime.UtcNow;

        // S17 FIX: AppDbContext está configurado com QueryTrackingBehavior.NoTracking
        // GLOBAL (App.xaml.cs) — toda consulta vem "solta", sem rastreio. Sem chamar
        // Update() explicitamente, SaveChangesAsync não tinha NADA marcado como
        // modificado e rodava "com sucesso" sem gravar nada no banco — exatamente o
        // bug real: agendar follow-up mostrava sucesso mas nunca persistia.
        _ctx.Orcamentos.Update(orc);
        await _ctx.SaveChangesAsync();
    }

    public async Task RegistrarContatoAsync(Guid id, RegistrarContatoDto dto)
    {
        var orc = await _ctx.Orcamentos.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted)
            ?? throw new KeyNotFoundException($"Orçamento {id} não encontrado.");

        orc.StatusFollowUp     = dto.StatusFollowUp;
        orc.ObservacaoFollowUp = dto.ObservacaoFollowUp;
        orc.DataUltimoContato  = DateTime.Now;
        orc.UpdatedAt          = DateTime.UtcNow;

        if (dto.StatusFollowUp == StatusFollowUp.Perdido)
        {
            orc.Status      = StatusOrcamento.Cancelado;
            orc.MotivoPerda = dto.MotivoPerda;
        }
        else if (dto.StatusFollowUp == StatusFollowUp.Convertido)
        {
            orc.Status = StatusOrcamento.Vendido;
        }
        else if (dto.ProximoFollowUpEmDias.HasValue && dto.ProximoFollowUpEmDias > 0)
        {
            // Reagenda automaticamente após o contato
            orc.DataFollowUp   = DateTime.Today.AddDays(dto.ProximoFollowUpEmDias.Value);
            orc.StatusFollowUp = StatusFollowUp.Pendente;
        }

        // S17 FIX: mesmo bug do AgendarFollowUpAsync — sem esse Update() explícito,
        // nada era realmente persistido (tracking global desligado no projeto).
        _ctx.Orcamentos.Update(orc);
        await _ctx.SaveChangesAsync();
    }

    public async Task<TaxaConversaoDto> GetTaxaConversaoAsync(DateTime inicio, DateTime fim)
    {
        var orcamentos = await _ctx.Orcamentos.AsNoTracking()
            .Where(o => !o.IsDeleted && o.DataEmissao >= inicio && o.DataEmissao <= fim)
            .ToListAsync();

        var total       = orcamentos.Count;
        var convertidos = orcamentos.Count(o => o.Status == StatusOrcamento.Vendido);
        var perdidos    = orcamentos.Count(o => o.Status == StatusOrcamento.Cancelado
                                             && o.StatusFollowUp == StatusFollowUp.Perdido);
        var pendentes   = orcamentos.Count(o => o.Status == StatusOrcamento.Aberto);

        var topMotivos = orcamentos
            .Where(o => !string.IsNullOrWhiteSpace(o.MotivoPerda))
            .GroupBy(o => o.MotivoPerda!)
            .Select(g => new MotivosPerdaDto { Motivo = g.Key, Quantidade = g.Count() })
            .OrderByDescending(m => m.Quantidade)
            .Take(5)
            .ToList();

        return new TaxaConversaoDto
        {
            TotalOrcamentos      = total,
            Convertidos          = convertidos,
            Perdidos             = perdidos,
            Pendentes            = pendentes,
            TaxaConversaoPercent = total > 0 ? Math.Round((decimal)convertidos / total * 100, 1) : 0,
            ValorConvertido      = orcamentos.Where(o => o.Status == StatusOrcamento.Vendido).Sum(o => o.ValorTotal),
            ValorPerdido         = orcamentos.Where(o => o.StatusFollowUp == StatusFollowUp.Perdido).Sum(o => o.ValorTotal),
            TopMotivosPerdas     = topMotivos
        };
    }

    public async Task<IEnumerable<Orcamento>> GetByClienteAsync(Guid clienteId)
{
    var todos = await ObterTodosAsync();
    return todos.Where(o => o.CustomerId == clienteId);
}

    private static OrcamentoFollowUpDto MapToFollowUpDto(Orcamento o) => new()
    {
        Id                 = o.Id,
        Numero             = o.Numero,
        ClienteNome        = o.CustomerName,
        VendedorNome       = o.SellerName,
        ValorTotal         = o.ValorTotal,
        DataEmissao        = o.DataEmissao,
        DataValidade       = o.DataValidade,
        Status             = o.Status.ToString(),
        DataFollowUp       = o.DataFollowUp,
        StatusFollowUp     = o.StatusFollowUp,
        MotivoPerda        = o.MotivoPerda,
        ObservacaoFollowUp = o.ObservacaoFollowUp,
        DataUltimoContato  = o.DataUltimoContato
    };
}