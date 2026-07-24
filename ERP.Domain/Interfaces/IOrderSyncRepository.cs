using ERP.Domain.Entities;
using ERP.Domain.Enums;

namespace ERP.Domain.Interfaces;

/// <summary>
/// Repositório único pro módulo de Integrações (Marketplace). Deliberadamente
/// não é um repositório por entidade (padrão do resto do projeto) — as 10
/// entidades desse módulo só têm um consumidor real hoje, o próprio motor de
/// processamento (OrderProcessingService). Fragmentar em 10 interfaces
/// seria boilerplate sem ganho de testabilidade, já que nenhuma delas é
/// consultada fora desse fluxo. Se isso mudar (uma tela de relatório própria
/// pedindo SkuMapping isolado, por exemplo), aí sim vale extrair.
/// </summary>
public interface IOrderSyncRepository
{
    // ── SalesChannel ────────────────────────────────────────────────
    Task<IReadOnlyList<SalesChannel>> GetCanaisAtivosAsync();
    Task<SalesChannel?> GetCanalByIdAsync(Guid id);

    /// <summary>Cria um novo canal pro tenant atual. TenantId é preenchido
    /// automaticamente pelo AppDbContext (PreencherTenantIdEUpdatedAt) — não
    /// precisa setar antes de chamar.</summary>
    Task<SalesChannel> AdicionarCanalAsync(SalesChannel canal);

    /// <summary>Última sessão de processamento (polling) desse canal, pra
    /// mostrar "última sincronização" na tela de Integrações.</summary>
    Task<ProcessingSession?> GetUltimaSessaoAsync(Guid salesChannelId);

    /// <summary>
    /// Mesma busca por Id, mas ignorando o filtro de tenant — uso restrito ao
    /// callback do OAuth (/ml/callback), que é [AllowAnonymous] de propósito
    /// (o Mercado Livre não devolve nosso JWT). Não existe tenant nesse momento
    /// pra aplicar o filtro normal. NUNCA usar isso num contexto autenticado —
    /// lá o GetCanalByIdAsync comum é o certo, senão um admin de um tenant
    /// poderia manipular o SalesChannel de outro só sabendo o Guid.
    /// </summary>
    Task<SalesChannel?> GetCanalPorIdSemFiltroAsync(Guid id);

    /// <summary>
    /// Salva os tokens OAuth com busca-e-grava dedicada (mesmo motivo do
    /// MarcarVendaGeradaAsync): o "canal" que o chamador já tem em mãos pode
    /// ter perdido o rastreamento por causa de um SaveChanges anterior no
    /// mesmo request (ex: ProcessarCanalAsync salva a ProcessingSession antes
    /// de chegar aqui). IgnoreQueryFilters porque esse método também é usado
    /// no callback anônimo do OAuth, onde ainda não existe tenant no contexto.
    /// </summary>
    Task AtualizarTokensAsync(Guid salesChannelId, string? accessToken, string? refreshToken,
        DateTime tokenExpiraEm, string? externalAccountId);

    /// <summary>
    /// Marca o pedido como VendaGerada com uma busca-e-grava dedicada, sempre
    /// com rastreamento garantido. Necessário porque SaveChangesAsync do
    /// projeto chama ChangeTracker.Clear() a cada gravação — se a venda
    /// (_saleService.CreateAsync) salvar no meio do caminho, o "pedido" que
    /// já estava em mãos perde o rastreamento e uma mutação direta nele vira
    /// no-op silencioso. Buscar de novo aqui garante que a gravação acontece.
    /// </summary>
    Task MarcarVendaGeradaAsync(Guid externalOrderId, Guid vendaId);

    /// <summary>DIAGNÓSTICO TEMPORÁRIO — remover depois de resolver a oscilação de connection string.</summary>
    string NomeDoBancoConectado();
    /// <summary>Resolve o canal pelo user_id/shop_id do marketplace — é assim que o
    /// tenant é identificado a partir do payload do webhook, não pela URL.</summary>
    Task<SalesChannel?> GetCanalPorContaExternaAsync(SalesChannelType tipo, string externalAccountId);

    // ── ExternalOrder / Itens ─────────────────────────────────────────
    Task<ExternalOrder?> GetExternalOrderAsync(Guid salesChannelId, string externalOrderId);
    /// <summary>
    /// Tenta inserir o pedido. Devolve false (sem lançar) se outra requisição
    /// concorrente já inseriu o mesmo (SalesChannelId, ExternalOrderId) um
    /// instante antes — o Mercado Livre manda o mesmo webhook várias vezes
    /// quase ao mesmo tempo, então essa corrida é esperada, não um erro real.
    /// </summary>
    Task<bool> TentarInserirExternalOrderAsync(ExternalOrder pedido);

    // ── SkuMapping ─────────────────────────────────────────────────────
    Task<SkuMapping?> GetSkuMappingAsync(Guid salesChannelId, string skuExterno);

    /// <summary>Cria um novo mapeamento SKU externo → Product.</summary>
    Task<SkuMapping> AdicionarMapeamentoAsync(SkuMapping mapeamento);

    /// <summary>Todos os mapeamentos de um canal, com o Product já incluído
    /// (pra tela de SkuMapping mostrar o nome do produto sem outra ida ao banco).</summary>
    Task<IReadOnlyList<SkuMapping>> GetMapeamentosPorCanalAsync(Guid salesChannelId);

    // ── Estoque sombra ───────────────────────────────────────────────
    /// <summary>Soma das reservas ativas (Status = Reservada) desse produto, em todos os canais.</summary>
    Task<decimal> GetTotalReservadoAsync(Guid productId);
    Task AddShadowStockReservationAsync(ShadowStockReservation reserva);
    /// <summary>Reservas ativas (Status = Reservada) de um pedido específico — pra confirmar/liberar depois.</summary>
    Task<IReadOnlyList<ShadowStockReservation>> GetReservasAtivasPorPedidoAsync(Guid externalOrderId);

    // ── Rastro (Event/Action/Conflict) ──────────────────────────────
    Task AddOrderEventAsync(OrderEvent evento);
    Task AddOrderActionAsync(OrderAction acao);
    Task AddOrderConflictAsync(OrderConflict conflito);

    // ── ProcessingSession ────────────────────────────────────────────
    Task AddProcessingSessionAsync(ProcessingSession sessao);

    // ── Cliente de repasse (sintético, ver SalesChannel.ClienteRepasseId) ──
    Task<Customer?> GetClienteRepasseAsync(Guid salesChannelId);
    Task<Customer> CriarClienteRepasseAsync(SalesChannel canal);

    /// <summary>
    /// Persiste tudo que foi rastreado/modificado nesta unidade de trabalho.
    /// Entidades buscadas por este mesmo repositório continuam rastreadas
    /// pelo EF Core — mutar um campo (ex: pedido.InternalStatus = X) e
    /// chamar isso é suficiente, não precisa de um Update() explícito por
    /// entidade (todas vêm do mesmo AppDbContext, escopo por request/job).
    /// </summary>
    Task<int> SalvarAsync();
}