// ── ERP.Application/Interfaces/IMotorFinanceiroService.cs ─────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

/// <summary>
/// Ponto único de entrada pro PDV processar o recebimento de uma venda. Antes,
/// essa decisão (Dinheiro → Caixa, PIX → Conta Bancária, Cartão → só Caixa por
/// enquanto, A Prazo → Conta a Receber, Haver → saldo do cliente) vivia inline
/// dentro do FinalizarVendaViewModel — cada forma de pagamento nova ou regra
/// que mudasse exigia editar a tela do PDV. Agora o PDV só chama
/// ProcessarRecebimentoVendaAsync; a regra de "pra onde vai cada forma de
/// pagamento" evolui aqui, sem o PDV precisar mudar de novo.
/// </summary>
public interface IMotorFinanceiroService
{
    Task ProcessarRecebimentoVendaAsync(
        Guid vendaId,
        Guid usuarioId,
        Guid? clienteId,
        string nomeCliente,
        string nomeVendedor,
        string nomeOperador,
        decimal troco,
        IEnumerable<(PaymentMethod Forma, decimal Valor)> pagamentos);

    /// <summary>
    /// Liquidação de um lote de Recebíveis de Operadora — passa pelo Motor
    /// Financeiro (não pela tela direto) pra manter um único ponto de entrada
    /// pra tudo que mexe em dinheiro, junto com o resto do fluxo de venda.
    /// </summary>
    Task RegistrarLiquidacaoOperadoraAsync(
        IReadOnlyList<Guid> recebivelIds, decimal valorRealDepositado, DateTime dataLiquidacao);

    /// <summary>
    /// Trava de segurança do cancelamento — chame ANTES de qualquer outra coisa
    /// (antes de restaurar estoque, antes de mudar status). Se a venda tiver
    /// algum Recebível já Liquidado, lança exceção e não faz nada: reverter
    /// automaticamente quebraria a Conciliação Bancária (dinheiro real já está
    /// no banco, o extrato do banco não vai "devolver" sozinho).
    /// </summary>
    Task VerificarPodeCancelarVendaAsync(Guid vendaId);

    /// <summary>
    /// Reverte os efeitos financeiros de uma venda cancelada: Dinheiro vira
    /// Sangria no Caixa, PIX vira Saída de estorno na Conta Bancária (nunca
    /// apaga a entrada original), Cartão Pendente vira Cancelado. Não trata
    /// Haver (já revertido dentro do CancelAsync da própria venda) nem A Prazo
    /// (contas a receber canceladas separadamente) — chame só DEPOIS de
    /// VerificarPodeCancelarVendaAsync não lançar exceção.
    /// </summary>
    Task EstornarVendaAsync(
        Guid vendaId, Guid usuarioId, string descricaoEstorno, decimal troco,
        IEnumerable<(PaymentMethod Forma, decimal Valor)> pagamentos);
}