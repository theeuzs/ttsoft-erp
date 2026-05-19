using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IProdutoAgregadoService
{
    /// <summary>
    /// Retorna os produtos sugeridos quando <paramref name="produtoPrincipalId"/>
    /// é adicionado ao carrinho. Já filtra por estoque > 0 e ordena por Ordem.
    /// Usado pelo PDV (WPF e Web) para exibir o popup de auto-sugestão.
    /// </summary>
    Task<IEnumerable<ProdutoAgregadoDto>> GetSugestoesAsync(Guid produtoPrincipalId);

    /// <summary>
    /// Retorna todos os produtos relacionados de um produto, incluindo
    /// os sem estoque. Usado pelo cadastro de produto para gerenciar a lista.
    /// </summary>
    Task<IEnumerable<ProdutoAgregadoDto>> GetAgregadosAsync(Guid produtoPrincipalId);

    /// <summary>
    /// Substitui completamente a lista de produtos relacionados.
    /// Operação idempotente: remove os que saíram, adiciona os novos, mantém os iguais.
    /// </summary>
    Task SalvarAgregadosAsync(Guid produtoPrincipalId, IEnumerable<SalvarAgregadoItemDto> itens);

    /// <summary>Remove um produto específico da lista de relacionados.</summary>
    Task RemoverAgregadoAsync(Guid produtoPrincipalId, Guid produtoRelacionadoId);
}
