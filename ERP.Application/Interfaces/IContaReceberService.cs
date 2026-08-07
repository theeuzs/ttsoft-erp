using ERP.Application.DTOs;
using ERP.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface IContaReceberService
{
    Task GerarContaAPrazoAsync(Guid clienteId, Guid? vendaId, decimal valor, string descricao, Guid? salePaymentId = null);

    /// <summary>Idempotência financeira granular (achado de auditoria pré-Fase-2
    /// do Offline-First, 08/2026) — checa por linha de pagamento específica.</summary>
    Task<bool> ExisteContaParaSalePaymentAsync(Guid salePaymentId);

    /// <summary>Idempotência (achado de auditoria pré-Fase-2 do Offline-First,
    /// 08/2026) — usado pelo MotorFinanceiroService pra checar se essa venda
    /// já gerou uma conta a prazo antes de criar outra.</summary>
    Task<IEnumerable<ContaReceber>> GetBySaleIdAsync(Guid saleId);

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

    /// <summary>Cancela a conta sem fingir que foi recebida — Status vira
    /// "Cancelado", ValorRecebido não muda. Diferente de "Zerar Saldo"
    /// (que era, na prática, DarBaixaTotalAsync disfarçado).</summary>
    Task CancelarAsync(Guid contaId, string motivo);

    /// <summary>Dá desconto numa conta específica — reduz o saldo devido sem
    /// contar como dinheiro recebido (ValorDesconto é campo separado).</summary>
    Task DarDescontoAsync(Guid contaId, decimal valorDesconto, string motivo);

    /// <summary>
    /// Baixa várias contas de uma vez (ex: cliente atendido por vendedores
    /// diferentes, quer quitar tudo junto no caixa). Quita da mais antiga pra
    /// mais nova (DataVencimento) até o valorAPagar acabar; o desconto é
    /// rateado proporcionalmente ao saldo de cada conta selecionada.
    /// </summary>
    Task DarBaixaEmLoteAsync(IEnumerable<Guid> contaIds, decimal valorAPagar, decimal valorDesconto, string formaPagamento);
    Task<(decimal TotalPendente, decimal TotalVencido, int QtdClientes)> GetResumoAsync();
    Task<int> CountInadimplentesAsync();

    /// <summary>Linha do tempo completa de uma conta — cada desconto, pagamento,
    /// cancelamento, e a criação original, em ordem cronológica.</summary>
    Task<IEnumerable<ContaReceberEvento>> GetEventosAsync(Guid contaId);

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