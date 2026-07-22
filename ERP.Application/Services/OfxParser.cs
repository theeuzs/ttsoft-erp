// ── ERP.Application/Services/OfxParser.cs ─────────────────────────────────────
using System.Globalization;
using System.Text.RegularExpressions;

namespace ERP.Application.Services;

public record OfxTransacaoDto(
    string   FitId,
    DateTime Data,
    decimal  Valor,
    string   Descricao);

/// <summary>
/// Leitor de extrato bancário em formato OFX (Open Financial Exchange).
/// Cobre os dois formatos em uso real pelos bancos brasileiros:
///   - OFX 1.x (SGML antigo, tags de campo sem fechamento — Ex: &lt;TRNAMT&gt;-150.00)
///   - OFX 2.x (XML válido, com fechamento — Ex: &lt;TRNAMT&gt;-150.00&lt;/TRNAMT&gt;)
/// Extração por regex em vez de parser SGML/XML completo — suficiente pro que
/// interessa aqui (linhas de STMTTRN), sem depender de biblioteca externa.
/// </summary>
public static class OfxParser
{
    private static readonly Regex BlocoTransacao =
        new(@"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex CampoFitId   = CampoRegex("FITID");
    private static readonly Regex CampoData    = CampoRegex("DTPOSTED");
    private static readonly Regex CampoValor   = CampoRegex("TRNAMT");
    private static readonly Regex CampoMemo    = CampoRegex("MEMO");
    private static readonly Regex CampoName    = CampoRegex("NAME");

    private static Regex CampoRegex(string tag)
        => new($@"<{tag}>\s*([^<\r\n]+)", RegexOptions.IgnoreCase);

    public static List<OfxTransacaoDto> Parse(string conteudoOfx)
    {
        var resultado = new List<OfxTransacaoDto>();

        foreach (Match bloco in BlocoTransacao.Matches(conteudoOfx))
        {
            var corpo = bloco.Groups[1].Value;

            var fitId = ExtrairCampo(corpo, CampoFitId);
            var data  = ExtrairData(corpo);
            var valor = ExtrairValor(corpo);

            if (data is null || valor is null) continue; // linha malformada — ignora, não trava o import inteiro

            var descricao = ExtrairCampo(corpo, CampoMemo);
            if (string.IsNullOrWhiteSpace(descricao))
                descricao = ExtrairCampo(corpo, CampoName);
            if (string.IsNullOrWhiteSpace(descricao))
                descricao = "(sem descrição no extrato)";

            resultado.Add(new OfxTransacaoDto(
                string.IsNullOrWhiteSpace(fitId) ? Guid.NewGuid().ToString("N") : fitId,
                data.Value,
                valor.Value,
                descricao.Trim()));
        }

        return resultado;
    }

    private static string ExtrairCampo(string corpo, Regex campo)
    {
        var m = campo.Match(corpo);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    private static DateTime? ExtrairData(string corpo)
    {
        var texto = ExtrairCampo(corpo, CampoData);
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // Formato padrão OFX: YYYYMMDDHHMMSS[.xxx][+TZ] — usa só os 8 primeiros dígitos (data).
        var soDigitos = new string(texto.TakeWhile(c => char.IsDigit(c)).ToArray());
        if (soDigitos.Length < 8) return null;

        return DateTime.TryParseExact(soDigitos[..8], "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;
    }

    private static decimal? ExtrairValor(string corpo)
    {
        var texto = ExtrairCampo(corpo, CampoValor);
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // OFX sempre usa ponto como separador decimal, independente da cultura local.
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }
}
