using ERP.Domain.Entities;
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

/// <summary>
/// Abstração de um marketplace específico (Mercado Livre, Shopee). Cada
/// implementação declara suas Capacidades (ChannelCapability) — o motor de
/// processamento consulta isso antes de tentar uma etapa que o canal não
/// suporta, em vez de precisar de uma interface própria por capacidade.
///
/// Sem framework de pipeline por trás disso: o motor que chama estes métodos
/// é um método sequencial com passos privados, no estilo do
/// MotorFinanceiroService — não uma cadeia de handlers genérica.
/// </summary>
public interface IChannelDispatcher
{
    SalesChannelType   Tipo        { get; }
    ChannelCapability  Capacidades { get; }

    /// <summary>Busca pedidos novos (ou atualizados) desde a última rodada.</summary>
    Task<(bool Sucesso, string Mensagem, IReadOnlyList<ExternalOrderDto> Pedidos)> BuscarPedidosNovosAsync(
        SalesChannel canal, DateTime desde);

    /// <summary>
    /// Busca um único pedido pelo id externo — usado pelo fluxo de webhook, que informa
    /// "o pedido X mudou" e não um intervalo de datas como o polling acima.
    /// </summary>
    Task<(bool Sucesso, string Mensagem, ExternalOrderDto? Pedido)> BuscarPedidoPorIdAsync(
        SalesChannel canal, string externalOrderId);

    /// <summary>Informa ao canal o novo status de um pedido já processado.</summary>
    Task<(bool Sucesso, string Mensagem)> AtualizarStatusPedidoAsync(
        SalesChannel canal, string externalOrderId, string novoStatusExterno);

    /// <summary>Envia o estoque sombra (Product.Stock − SkuMapping.BufferSeguranca) ao canal.</summary>
    Task<(bool Sucesso, string Mensagem)> SincronizarEstoqueAsync(
        SalesChannel canal, IReadOnlyList<(string SkuExterno, decimal Quantidade)> estoques);

    /// <summary>Lista os anúncios ativos do vendedor no canal — usado pela tela
    /// de SkuMapping pra saber o que existe do lado do marketplace, sem
    /// precisar que o lojista digite o SKU externo manualmente.</summary>
    Task<(bool Sucesso, string Mensagem, IReadOnlyList<AnuncioExternoDto> Anuncios)> BuscarAnunciosAsync(
        SalesChannel canal);
}

/// <summary>Representação normalizada de um pedido externo, já traduzida do formato cru do canal.</summary>
public class ExternalOrderDto
{
    public string   ExternalOrderId  { get; set; } = string.Empty;
    public string   ExternalStatus   { get; set; } = string.Empty;
    public DateTime DataPedidoExterno { get; set; }
    public decimal  ValorTotal        { get; set; }
    public string?  RawPayloadJson    { get; set; }
    public List<ExternalOrderItemDto> Itens { get; set; } = new();
}

public class ExternalOrderItemDto
{
    public string  SkuExterno    { get; set; } = string.Empty;
    public string? ItemId        { get; set; } // ex: MLB4935405945 — fallback quando o anúncio não tem SkuExterno
    public string  DescricaoItem { get; set; } = string.Empty;
    public decimal Quantidade    { get; set; }
    public decimal ValorUnitario { get; set; }
}

/// <summary>Um anúncio ativo do vendedor no marketplace, normalizado — usado
/// pra tela de SkuMapping mostrar o que existe no canal antes de mapear.</summary>
public class AnuncioExternoDto
{
    public string ItemId     { get; set; } = string.Empty; // ex: MLB4935405945
    public string SkuExterno { get; set; } = string.Empty; // seller_custom_field — o que bate com SkuMapping
    public string Titulo     { get; set; } = string.Empty;
}