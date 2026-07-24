// ── ERP.Application/Interfaces/IEstoqueSyncService.cs ──────────────────────
namespace ERP.Application.Interfaces;

/// <summary>
/// Sincroniza estoque ERP → marketplace. Direção contrária de
/// IOrderProcessingService, que traz pedido do marketplace pro ERP — esse
/// aqui avisa o marketplace quando o estoque muda por qualquer motivo do
/// lado de cá (venda no PDV, ajuste manual, devolução, etc.), pra não vender
/// no marketplace um produto que já zerou aqui.
/// </summary>
public interface IEstoqueSyncService
{
    /// <summary>
    /// Sincroniza um produto específico em todos os canais onde ele está
    /// mapeado. Best-effort: nunca lança — uma falha de rede/API externa não
    /// deveria derrubar quem chamou (ex: uma venda no PDV não deve falhar
    /// porque o Mercado Livre está fora do ar).
    /// </summary>
    Task SincronizarProdutoAsync(Guid productId);
}
