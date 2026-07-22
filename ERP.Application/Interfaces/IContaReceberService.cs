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

    // S15 FIX: movido de ContasReceberController.GerarBoleto — controller não
    // deve tocar AppDbContext/AsaasService diretamente.
    /// <summary>Gera (ou retorna existente) boleto bancário via Asaas para uma conta a receber.</summary>
    Task<GerarBoletoResultado> GerarBoletoAsync(Guid contaId);
}

/// <summary>Status possíveis do resultado de GerarBoletoAsync — controller mapeia 1:1 para HTTP status.</summary>
public enum GerarBoletoStatus
{
    ContaNaoEncontrada,
    JaPossuiBoleto,
    ClienteNaoVinculado,
    ClienteSemDocumento,
    FalhaAoRegistrarClienteAsaas,
    FalhaAoGerarBoleto,
    // S17 FIX: AsaasService virou opcional no construtor de ContaReceberService
    // (o WPF nunca registrou esse serviço no próprio DI — boleto via Asaas
    // sempre foi feature só de API/Portal). Esse status cobre o caso de alguém
    // tentar gerar boleto num ambiente onde o Asaas não está disponível.
    AsaasIndisponivel,
    Sucesso
}

public record GerarBoletoResultado(
    GerarBoletoStatus Status,
    string?           Erro          = null,
    string?           BoletoUrl     = null,
    string?           InvoiceUrl    = null,
    string?           BoletoBarCode = null,
    string?           AsaasStatus   = null);