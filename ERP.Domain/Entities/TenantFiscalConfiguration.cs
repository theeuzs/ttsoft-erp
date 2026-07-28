// ── ERP.Domain/Entities/TenantFiscalConfiguration.cs ────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Configuração fiscal por tenant — Etapa 2 da refatoração fiscal. Antes
/// vivia num arquivo local (config_recibo.json) na máquina do WPF, ilegível
/// pela API. Só os dois campos que a emissão de verdade consome hoje —
/// nada de CSC/Série especulativos (o Focus NFe cuida disso do lado dele).
/// </summary>
public class TenantFiscalConfiguration : BaseEntity
{
    /// <summary>Criptografado com TokenProtector (AES portátil — não DPAPI,
    /// que só funciona no Windows/usuário local e não seria legível pela API).</summary>
    public string TokenFocusNfeEncriptado { get; set; } = string.Empty;

    public bool UsarAmbienteProducao { get; set; } = false;
}
