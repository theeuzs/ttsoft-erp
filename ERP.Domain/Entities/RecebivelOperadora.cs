// ── ERP.Domain/Entities/RecebivelOperadora.cs ─────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Dinheiro de uma venda em cartão que está "no limbo" entre a operadora
/// (Stone/Cielo/etc.) e a conta bancária da loja — vendido hoje, mas só vira
/// saldo de verdade quando a operadora liquida (deposita). Resolve o problema
/// identificado nesta sessão: cartão não pode cair direto em ContaBancaria como
/// se fosse PIX, porque na vida real ele fica retido dias, em lote, com taxa
/// descontada.
/// </summary>
public class RecebivelOperadora : BaseEntity
{
    public Guid OperadoraRecebimentoId { get; set; }
    public OperadoraRecebimento? OperadoraRecebimento { get; set; }

    /// <summary>Venda que originou esse recebível — só referência, não obrigatório.</summary>
    public Guid? VendaId { get; set; }

    public FormaRecebimentoOperadora FormaRecebimento { get; set; }

    public decimal ValorBruto   { get; set; } // valor da venda, o que o cliente pagou
    public decimal ValorTaxa    { get; set; } // taxa da operadora, calculada na criação
    public decimal ValorLiquido { get; set; } // ValorBruto - ValorTaxa — o que deve cair no banco

    public DateTime  DataVenda              { get; set; } = DateTime.Now;
    public DateTime  DataPrevistaLiquidacao { get; set; }

    public StatusRecebivel Status         { get; set; } = StatusRecebivel.Pendente;
    public DateTime?       DataLiquidacao { get; set; }

    /// <summary>Movimento criado na Conta Bancária quando esse recebível (ou o lote que o contém) foi liquidado.</summary>
    public Guid? MovimentoContaBancariaId { get; set; }

    /// <summary>
    /// NSU/código de autorização da maquininha — preparado (Categoria B), ainda
    /// não preenchido por nenhum fluxo. O PDV não recebe esse dado do TEF hoje;
    /// fica pronto pro dia em que essa captura existir, sem migration nova.
    /// </summary>
    public string? Nsu { get; set; }
}