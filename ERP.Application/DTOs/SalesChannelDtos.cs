// ── ERP.Application/DTOs/SalesChannelDtos.cs ───────────────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

/// <summary>Corpo do POST /api/saleschannels — genérico por design: um único
/// endpoint pra conectar qualquer marketplace (Mercado Livre hoje, Shopee/
/// Amazon/Magalu depois), sem precisar de uma rota nova por plataforma.</summary>
public record CriarSalesChannelDto(SalesChannelType Tipo, string Nome, bool Ativo = true);

/// <summary>Resposta do POST — AuthorizationUrl vem null pra tipos que ainda
/// não têm um fluxo de auto-atendimento implementado (hoje só Mercado Livre tem).</summary>
public record SalesChannelCriadoDto(Guid Id, string? AuthorizationUrl);

/// <summary>Um item da tela "Integrações" — o suficiente pra mostrar o card
/// de status de cada canal sem o WPF precisar montar a lógica sozinho.</summary>
public record SalesChannelStatusDto(
    Guid Id,
    SalesChannelType Tipo,
    string Nome,
    bool Conectado,
    string? ExternalAccountId,
    DateTime? TokenExpiraEm,
    bool TokenValido,
    DateTime? UltimaSincronizacao,
    int? UltimoTotalProcessados);
