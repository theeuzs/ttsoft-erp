// ── ERP.Domain/Enums/MarketplaceEnums.cs ──────────────────────────────────────
namespace ERP.Domain.Enums;

public enum SalesChannelType
{
    MercadoLivre = 1,
    Shopee       = 2
}

/// <summary>
/// O que um SalesChannel sabe fazer. Um IChannelDispatcher declara suas próprias
/// capacidades aqui — permite o ProcessingSession pular etapas que o canal não
/// suporta (ex: Shopee sem push de estoque na v1) sem precisar de outra interface.
/// </summary>
[Flags]
public enum ChannelCapability
{
    None            = 0,
    RecebePedidos   = 1,
    AtualizaStatus  = 2,
    SincronizaEstoque = 4,
    PoliticaDePreco = 8
}

/// <summary>
/// Status interno do pedido — vocabulário nosso, independente do texto que cada
/// marketplace usa (isso fica em ExternalOrder.ExternalStatus, cru, sem tradução).
/// </summary>
public enum ExternalOrderStatus
{
    Recebido             = 1,
    AguardandoSku        = 2, // ItemExterno sem SkuMapping — bloqueado até alguém mapear
    EstoqueReservado     = 3,
    VendaGerada          = 4,
    ConflitoAberto        = 5,
    Cancelado            = 6,
    Concluido            = 7
}

public enum OrderEventType
{
    PedidoRecebido       = 1,
    SkuResolvido         = 2,
    EstoqueReservado     = 3,
    VendaGerada          = 4,
    ConflitoDetectado    = 5,
    ConflitoResolvido    = 6,
    StatusEnviadoAoCanal = 7,
    PedidoCancelado      = 8
}

public enum OrderActionType
{
    ResolverSku          = 1,
    ReservarEstoque       = 2,
    GerarVendaInterna     = 3,
    NotificarStatusCanal  = 4
    // Sincronizar estoque NÃO entra aqui — não é uma ação por pedido, é uma
    // operação por produto/canal (OrderAction sempre está amarrado a um
    // ExternalOrderId). Fica fora do escopo desta entidade até existir um
    // consumidor real de log próprio para isso.
}

public enum OrderActionStatus
{
    Pendente  = 1,
    Concluida = 2,
    Falhou    = 3
}

/// <summary>Nível de severidade de um OrderEvent — permite a UI colorir sem interpretar texto.</summary>
public enum OrderEventSeverity
{
    Info    = 1,
    Warning = 2,
    Error   = 3
}

/// <summary>Como um OrderConflict foi encerrado — só preenchido quando Resolvido = true.</summary>
public enum TipoResolucaoConflito
{
    Manual       = 1,
    Automatica   = 2,
    Ignorada     = 3,
    Reprocessada = 4
}

/// <summary>Ciclo de vida de uma reserva de estoque sombra.</summary>
public enum StatusReservaEstoque
{
    Reservada  = 1,
    Confirmada = 2, // virou baixa real de estoque (Sale gerada)
    Liberada   = 3, // cancelada/expirada sem virar venda
    Expirada   = 4
}

/// <summary>Motivo de falha de uma OrderAction — usado em vez de string livre.</summary>
public enum ProcessingErrorCode
{
    SkuNaoMapeado         = 1,
    EstoqueInsuficiente   = 2,
    ClienteInvalido       = 3,
    ErroComunicacaoCanal  = 4,
    PedidoDuplicado       = 5,
    ErroDesconhecido      = 99
}

public enum OrderConflictType
{
    SkuNaoMapeado        = 1,
    EstoqueInsuficiente  = 2,
    PedidoDuplicado      = 3,
    DivergenciaDePreco   = 4
}

public enum ProcessingSessionStatus
{
    EmAndamento         = 1,
    Concluido           = 2,
    ConcluidoComErros   = 3
}
