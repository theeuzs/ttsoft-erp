// ── ERP.Application/DTOs/NfeRecebidaDto.cs ──────────────────────────────────
namespace ERP.Application.DTOs;

public record NfeRecebidaDto(
    Guid Id, string Chave, string? CnpjEmitente, string? NomeEmitente,
    DateTime? DataEmissao, decimal? ValorTotal, string StatusManifestacao,
    bool Importada, DateTime DescobertaEm);
