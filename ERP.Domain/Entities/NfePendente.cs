using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>Achado de auditoria (06/08/2026): essa entidade não tinha
/// TenantId nenhum (nem herdado) — dependia inteiramente de filtro manual
/// em cada consumidor, sem rede de proteção nenhuma. Passou a herdar
/// BaseEntity, igual toda outra entidade "de fila"/rascunho do sistema.</summary>
public class NfePendente : BaseEntity
{
    public Guid VendaId { get; set; }
    public string TipoNota { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public DateTime DataFalha { get; set; }
    public int Tentativas { get; set; }
    public string? UltimaMensagemErro { get; set; }

    // ── Fila de recuperacao fiscal (Etapa 4A-4) ───────────────────────────
    // Colunas aditivas, todas com valor padrao no banco (ver migration), para
    // o codigo antigo continuar inserindo sem conhece-las. `Tentativas`
    // (acima) e legado e NAO e tocada por esta etapa.
    //
    // As datas novas sao UTC (decisao D-D), ao contrario de DataFalha
    // (horario de Brasilia, convencao historica). O EF devolve DateTime com
    // Kind Unspecified: quem converter para DateTimeOffset deve usar
    // DateTime.SpecifyKind(valor, DateTimeKind.Utc).

    /// <summary>"Ativa" (padrao), "AguardandoCorrecao" ou "IntervencaoManual".
    /// Constantes em ERP.Application.Fiscal.Recovery.NfePendenteEstados; este
    /// projeto nao referencia Application, entao o valor inicial e literal
    /// (um teste garante que os dois nao divergem).</summary>
    public string Estado { get; set; } = "Ativa";

    /// <summary>UTC. Nulo = elegivel agora.</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    /// <summary>Veredito + acao da ultima decisao. Texto curto: nunca o corpo bruto da Focus.</summary>
    public string? UltimaDecisao { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? UltimaConsultaEm { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? UltimoPostEm { get; set; }

    /// <summary>Cumulativo, para auditoria.</summary>
    public int TentativasPost { get; set; }

    /// <summary>Cumulativo, para auditoria.</summary>
    public int TentativasConsulta { get; set; }

    /// <summary>Base do backoff: +1 so em falha transitoria; zera em qualquer resultado nao transitorio.</summary>
    public int FalhasTransitoriasSeguidas { get; set; }

    /// <summary>Base do limite K: +1 so em resultado desconhecido; zera em qualquer resultado conhecido.</summary>
    public int FalhasDesconhecidasSeguidas { get; set; }
}