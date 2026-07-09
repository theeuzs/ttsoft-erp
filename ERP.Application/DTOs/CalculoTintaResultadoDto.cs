// ── ERP.Application/DTOs/CalculoTintaResultadoDto.cs ─────────────────────────
namespace ERP.Application.DTOs;

public record LatasDto(
    decimal Galoes18L,
    decimal Galoes3_6L,
    decimal Latas900ml,
    decimal TotalLitros);

public record CalculoTintaResultadoDto(
    string   Produto,
    string   NomeCor,
    string   CodigoFabricante,
    decimal  AreaM2,
    int      Demaos,
    decimal  RendimentoM2PorLitro,
    decimal  LitrosNecessarios,
    LatasDto Latas,
    decimal  CustoEstimado,
    string?  CorantesJson);
