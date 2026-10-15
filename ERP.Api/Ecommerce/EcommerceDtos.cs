namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - contrato da API (consumido SOMENTE pelo servidor do site, nunca pelo navegador).

/// <summary>O site manda os SKUs que quer exibir/vender e o preco que ele pretende cobrar.</summary>
public sealed record ConsultaCatalogoRequest(List<ItemConsultaCatalogo>? Itens);

public sealed record ItemConsultaCatalogo(string Sku, decimal PrecoVenda, int Quantidade = 1);

public sealed record ConsultaCatalogoResponse(DateTimeOffset GeradoEm, List<ItemCatalogoResposta> Itens);

/// <summary>
/// Custo e repasse NUNCA saem daqui. O site so recebe se pode publicar e quanto pode vender.
/// "Motivo" e diagnostico para o servidor do site registrar em log; nao deve ser exibido ao cliente.
/// </summary>
public sealed record ItemCatalogoResposta(
    string Sku,
    bool Encontrado,
    bool Publicavel,
    string? Motivo,
    string? Nome,
    string? Descricao,
    string? ImagemUrl,
    bool Disponivel,
    int QuantidadeMaxima);

/// <summary>
/// S{N} FIX: busca do painel do site (adicionar produto pelo codigo ou nome).
/// Traz o preco de balcao e o PISO de venda no site (repasse x margem minima), NUNCA o custo.
/// So o servidor do site recebe isto, e ele so mostra dentro do painel com login.
/// </summary>
public sealed record ProdutoBuscaResposta(
    string Codigo,
    string? Sku,
    string? Barcode,
    string Nome,
    string? Descricao,
    string? ImagemUrl,
    string? Categoria,
    string? Marca,
    string Unidade,
    decimal PrecoLoja,
    decimal? PrecoMinimo,
    string? Motivo,
    int EstoqueVendavel);

public sealed record BuscaProdutosResponse(DateTimeOffset GeradoEm, List<ProdutoBuscaResposta> Itens);

// -------------------------------------------------------------------------------------------------
// S{N} FIX: lancamento da venda do site no ERP (pedido marcado como "Pago" no painel do site).
// -------------------------------------------------------------------------------------------------

/// <summary>
/// PedidoId e o id unico do pedido no site (vira o Id da venda, por hash: chamar duas vezes nao duplica).
/// Linhas ja vem expandidas pelo site (kit vira um SKU por componente; lote vira a quantidade em unidades).
/// </summary>
public sealed record LancarPedidoRequest(string? PedidoId, string? Numero, List<LinhaPedidoEcommerce>? Linhas);

public sealed record LinhaPedidoEcommerce(string Sku, int Quantidade);

/// <summary>Repasse = total da venda no ERP (custo efetivo x FatorRepasse). So o painel do site (com login) mostra.</summary>
public sealed record LancarPedidoResponse(
    Guid VendaId,
    string NumeroVenda,
    decimal Repasse,
    bool JaExistia,
    bool ContaRepasseCriada);

public sealed record EstornarPedidoRequest(string? PedidoId, string? Motivo);

public sealed record EstornarPedidoResponse(bool Estornado, string Mensagem);
