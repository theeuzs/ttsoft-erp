// ── ERP.Application/Interfaces/IRecebivelOperadoraService.cs ─────────────────
using ERP.Application.DTOs;
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

public interface IRecebivelOperadoraService
{
    Task<IReadOnlyList<RecebivelOperadoraDto>> ObterPendentesAsync();

    /// <summary>
    /// Chamado pelo Motor Financeiro quando uma venda em cartão é finalizada.
    /// Não faz nada (silenciosamente) se não houver Operadora Padrão configurada —
    /// não trava a venda por causa de configuração financeira pendente.
    /// </summary>
    Task RegistrarRecebivelVendaAsync(Guid? vendaId, PaymentMethod formaPagamento, decimal valorBruto);

    /// <summary>
    /// Liquida um lote de recebíveis de uma vez — o valor real que caiu no banco
    /// nesse depósito (pode diferir da soma exata dos líquidos calculados, por
    /// arredondamento da operadora). Cria o movimento na Conta Bancária e marca
    /// todos os recebíveis selecionados como Liquidado.
    /// </summary>
    Task LiquidarLoteAsync(IReadOnlyList<Guid> recebivelIds, decimal valorRealDepositado, DateTime dataLiquidacao);

    /// <summary>true se a venda tem algum recebível JÁ LIQUIDADO — bloqueia cancelamento automático.</summary>
    Task<bool> TemLiquidadoPorVendaAsync(Guid vendaId);

    /// <summary>Cancela os recebíveis ainda pendentes de uma venda cancelada.</summary>
    Task CancelarPendentesPorVendaAsync(Guid vendaId);
}