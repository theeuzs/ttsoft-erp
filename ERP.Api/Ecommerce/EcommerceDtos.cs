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
