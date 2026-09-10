namespace ERP.Domain.Common;

/// <summary>
/// S21 FIX (17/08) — DateTime.Now/DateTimeOffset.Now dependem do fuso
/// horário AMBIENTE do sistema operacional onde o código roda. Isso é
/// péssimo pra um servidor na nuvem: o Azure App Service pode ter o
/// relógio em UTC mas o TimeZoneInfo.Local em cache com outro offset (ou
/// vice-versa) — descoberto quando isso causou rejeição SEFAZ 703 (data
/// de emissão da NFC-e saindo 3h no futuro) depois de um redeploy.
///
/// Este helper calcula a hora do Brasil de forma explícita, a partir de
/// DateTime.UtcNow (que é sempre correto, não importa o servidor) — o
/// resultado fica certo não importa como o SO esteja configurado.
/// </summary>
public static class FusoBrasilHelper
{
    public static readonly TimeZoneInfo FusoBrasil = ObterFusoBrasil();

    private static TimeZoneInfo ObterFusoBrasil()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); } // Linux/ICU
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); } // Windows
            catch (TimeZoneNotFoundException)
            {
                // Brasil não observa mais horário de verão desde 2019 — fuso
                // fixo -03:00 é um fallback seguro se nenhum dos dois IDs existir.
                return TimeZoneInfo.CreateCustomTimeZone("BR-fixo", TimeSpan.FromHours(-3), "Brasília (fixo)", "Brasília (fixo)");
            }
        }
    }

    /// <summary>Data/hora atual, correta pro Brasil, como DateTime "solto"
    /// (Kind=Unspecified) — pra guardar em colunas datetime2 já no horário
    /// certo, sem depender do fuso do servidor.</summary>
    public static DateTime AgoraNoBrasil()
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoBrasil);

    /// <summary>Achado (09/09) — migração pra Azure revelou esse gap: campos
    /// salvos corretamente em UTC (ex: Product.SalePriceChangedAt, via
    /// DateTime.UtcNow) apareciam 3h adiantados na tela, porque nada convertia
    /// de volta pro horário do Brasil na hora de EXIBIR — só existia o "agora",
    /// não "essa data específica que já está salva". Antes da migração,
    /// mascarado porque o WPF rodava no PC da loja com banco local, e o
    /// "servidor" (SQL Express local) coincidentemente já estava no fuso do
    /// Brasil.</summary>
    public static DateTime ConverterParaBrasil(DateTime dataUtc)
    {
        // Se a data já veio "solta" (Kind=Unspecified, comum vindo do EF
        // Core/SQL Server), assume que é UTC mesmo — é a convenção usada
        // em todo o projeto (AgoraNoBrasil() também devolve Unspecified).
        var utc = dataUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dataUtc, DateTimeKind.Utc)
            : dataUtc.ToUniversalTime();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, FusoBrasil);
    }

    /// <summary>Data/hora atual, correta pro Brasil, formatada do jeito que
    /// a Focus/SEFAZ espera (com offset explícito) — usa DateTimeOffset
    /// (carrega o offset junto do valor) em vez de DateTime+"zzz" (que pega
    /// o offset do sistema operacional, não necessariamente o do Brasil).</summary>
    public static string AgoraNoBrasilComOffset()
        => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FusoBrasil).ToString("yyyy-MM-ddTHH:mm:sszzz");
}