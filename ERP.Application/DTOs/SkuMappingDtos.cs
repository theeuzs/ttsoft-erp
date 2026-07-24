// ── ERP.Application/DTOs/SkuMappingDtos.cs ─────────────────────────────────
namespace ERP.Application.DTOs;

/// <summary>Um anúncio do marketplace, já dizendo se está mapeado ou não —
/// a tela usa isso pra separar "mapeado" de "precisa de atenção".</summary>
public record AnuncioComMapeamentoDto(
    string ItemId, string SkuExterno, string Titulo,
    bool Mapeado, Guid? ProductId, string? ProductNome);

/// <summary>Um mapeamento já existente, com o nome do produto pronto pra exibir.</summary>
public record SkuMappingDto(Guid Id, string SkuExterno, Guid ProductId, string ProductNome);

/// <summary>Corpo do POST de criação de mapeamento. Manda SkuExterno E/OU
/// ItemId — o backend decide qual guardar (prefere SkuExterno quando o
/// anúncio tem; usa ItemId como identificador quando não tem).</summary>
public record CriarSkuMappingDto(string? SkuExterno, string? ItemId, Guid ProductId, decimal? BufferSeguranca = null);