// ── ERP.Application/Services/EstoqueSyncService.cs ──────────────────────────
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using Serilog;

namespace ERP.Application.Services;

public class EstoqueSyncService : IEstoqueSyncService
{
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<IChannelDispatcher> _dispatchers;

    public EstoqueSyncService(IUnitOfWork uow, IEnumerable<IChannelDispatcher> dispatchers)
    {
        _uow         = uow;
        _dispatchers = dispatchers;
    }

    public async Task SincronizarProdutoAsync(Guid productId)
    {
        try
        {
            var mapeamentos = await _uow.OrderSync.GetMapeamentosPorProdutoAsync(productId);
            if (mapeamentos.Count == 0) return; // produto não anunciado em nenhum marketplace — nada a fazer

            var product = await _uow.Products.GetByIdAsync(productId);
            if (product is null) return;

            // Um produto pode estar anunciado em mais de um canal (ou mais de
            // um anúncio no mesmo canal) — agrupa pra mandar um PUT por
            // anúncio, mas resolver o canal/dispatcher só uma vez por grupo.
            var porCanal = mapeamentos
                .Where(m => !string.IsNullOrEmpty(m.ItemId) && m.SalesChannel is not null && m.SalesChannel.IsAtivo)
                .GroupBy(m => m.SalesChannelId);

            foreach (var grupo in porCanal)
            {
                var canal = grupo.First().SalesChannel!;
                var dispatcher = _dispatchers.FirstOrDefault(d => d.Tipo == canal.Tipo);
                if (dispatcher is null) continue; // canal sem dispatcher implementado (ex: Shopee) — pula

                var estoques = grupo
                    .Select(m => (m.ItemId!, Math.Max(0, product.Stock - m.BufferSeguranca)))
                    .ToList();

                var (sucesso, mensagem) = await dispatcher.SincronizarEstoqueAsync(canal, estoques);
                if (!sucesso)
                    Log.Warning(
                        "Falha ao sincronizar estoque do produto {ProductId} pro canal {CanalId} ({CanalTipo}): {Mensagem}",
                        productId, canal.Id, canal.Tipo, mensagem);
            }
        }
        catch (Exception ex)
        {
            // Best-effort de propósito — sincronização de estoque não pode
            // derrubar quem chamou (uma venda no PDV não deveria falhar por
            // causa do Mercado Livre estar fora do ar).
            Log.Error(ex, "Erro inesperado ao sincronizar estoque do produto {ProductId}", productId);
        }
    }
}