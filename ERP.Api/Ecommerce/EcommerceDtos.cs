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
