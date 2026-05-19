// ── Adicionar em ERP.Application/DTOs/Dtos.cs ─────────────────────────────────
// Cole estes records junto aos outros DTOs de produto no arquivo Dtos.cs existente.
// Separado aqui apenas para facilitar a aplicação do patch.

namespace ERP.Application.DTOs;

/// <summary>
/// Produto relacionado exibido no popup de auto-sugestão do PDV.
/// Contém apenas o necessário para renderizar o card de sugestão rapidamente.
/// </summary>
public record ProdutoAgregadoDto(
    Guid    Id,
    Guid    ProdutoRelacionadoId,
    string  Nome,
    string? Barcode,
    string  Unit,
    decimal Preco,
    decimal Estoque,
    string? ImageUrl,
    int     Ordem
)
{
    /// <summary>True quando o produto tem estoque disponível para venda.</summary>
    public bool EmEstoque => Estoque > 0;
}

/// <summary>Item enviado ao salvar/atualizar a lista de relacionados de um produto.</summary>
public record SalvarAgregadoItemDto(Guid ProdutoRelacionadoId, int Ordem);

/// <summary>Payload do endpoint PUT /api/products/{id}/agregados.</summary>
public record SalvarAgregadosDto(List<SalvarAgregadoItemDto> Itens);
