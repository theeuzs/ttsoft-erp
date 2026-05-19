using ERP.Application.DTOs;
using ERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface IContaReceberService
{
    Task GerarContaAPrazoAsync(Guid clienteId, Guid? vendaId, decimal valor, string descricao);

    // ── Parcelamento ──────────────────────────────────────────────────────────
    /// <summary>
    /// Gera N parcelas automaticamente para uma venda a prazo.
    /// Cada parcela é uma ContaReceber independente com vencimento escalonado.
    /// </summary>
    Task<IEnumerable<ParcelaDto>> GerarParcelasAsync(GerarParcelasDto dto);
    Task<IEnumerable<ParcelaDto>> GetParcelasByParcelamentoAsync(Guid parcelamentoId);
    Task<IEnumerable<ParcelaDto>> GetParcelasByVendaAsync(Guid vendaId);

    // ── Consultas ─────────────────────────────────────────────────────────────
    Task<IEnumerable<ContaReceber>> GetPendentesAsync();
    Task<IEnumerable<ContaReceber>> GetPorClienteAsync(Guid clienteId);
    Task<IEnumerable<ContaReceber>> GetInadimplentesAsync();
    Task DarBaixaParcialAsync(Guid contaId, decimal valorRecebido);
    Task DarBaixaTotalAsync(Guid contaId);
    Task<(decimal TotalPendente, decimal TotalVencido, int QtdClientes)> GetResumoAsync();
    Task<int> CountInadimplentesAsync();
}
