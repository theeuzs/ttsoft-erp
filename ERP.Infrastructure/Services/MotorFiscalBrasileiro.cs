using ERP.Domain.Services.Fiscal;
namespace ERP.Infrastructure.Services;

/// <summary>
/// Motor fiscal brasileiro com tabelas ICMS por UF.
/// Permite calcular impostos sem dependência de APIs externas.
/// Base para futura emissão direta de NF-e/NFC-e via SEFAZ.
/// </summary>
public static class MotorFiscalBrasileiro
{
    // ── Alíquotas interestaduais (Resolução SF 22/1989) ──────────────────────

    private static readonly Dictionary<string, decimal> _aliqInterestadual = new()
    {
        // Sul e Sudeste → qualquer estado: 12%
        { "SP-MG", 12m }, { "SP-RS", 12m }, { "SP-SC", 12m }, { "SP-PR", 12m },
        { "MG-SP", 12m }, { "RS-SP", 12m }, { "SC-SP", 12m }, { "PR-SP", 12m },
        // Demais combinações interestaduais: 7%
    };

    /// <summary>Retorna alíquota interestadual de ICMS entre dois estados.</summary>
    public static decimal ObterAliquotaInterestadual(string ufOrigem, string ufDestino)
    {
        if (ufOrigem == ufDestino) return 0; // Operação interna

        var estados_sul_sudeste = new[] { "SP", "MG", "RJ", "ES", "RS", "SC", "PR" };
        var origemSulSudeste = estados_sul_sudeste.Contains(ufOrigem.ToUpper());
        var destinoSulSudeste = estados_sul_sudeste.Contains(ufDestino.ToUpper());

        // SP/MG/RS/SC/PR entre si: 12%; para demais: 7%
        return origemSulSudeste && destinoSulSudeste ? 12m : 7m;
    }

    // ── Alíquotas internas por UF ─────────────────────────────────────────────

    private static readonly Dictionary<string, decimal> _aliqInterna = new()
    {
        { "AC", 17m }, { "AL", 17m }, { "AP", 18m }, { "AM", 20m },
        { "BA", 20.5m }, { "CE", 20m }, { "DF", 20m }, { "ES", 17m },
        { "GO", 17m }, { "MA", 22m }, { "MT", 17m }, { "MS", 17m },
        { "MG", 18m }, { "PA", 17m }, { "PB", 18m }, { "PR", 19.5m },
        { "PE", 20.5m }, { "PI", 21m }, { "RJ", 22m }, { "RN", 18m },
        { "RS", 17.5m }, { "RO", 17.5m }, { "RR", 20m }, { "SC", 17m },
        { "SP", 18m }, { "SE", 19m }, { "TO", 20m }
    };

    public static decimal ObterAliquotaInterna(string uf)
        => _aliqInterna.TryGetValue(uf.ToUpper(), out var aliq) ? aliq : 17m;

    // ── Cálculo ICMS Simples Nacional ─────────────────────────────────────────

    public static ResultadoFiscal CalcularImpostos(ProdutoFiscal produto, string ufDestino)
    {
        var aliqInterna = ObterAliquotaInterna(ufDestino);
        var baseCalc    = produto.ValorProduto;

        // ICMS no Simples: apenas destaque se CSOSN 101 ou 500 com ST
        decimal icms = 0m, icmsST = 0m, baseCalcST = 0m;

        if (produto.CSOSN is "101" or "201")
        {
            // Aproveitamento de crédito — alíquota nominal
            icms = Math.Round(baseCalc * (produto.AliquotaICMS / 100m), 2);
        }

        // ICMS-ST se produto tem substituição tributária
        if (produto.TemST && produto.MVA.HasValue)
        {
            var calc = new ICMSSTCalculator();
            var result = calc.Calcular(
                produto.ValorProduto,
                aliqInterna,
                produto.MVA.Value,
                produto.UfOrigem != ufDestino
                    ? ObterAliquotaInterestadual(produto.UfOrigem, ufDestino)
                    : 0m,
                produto.AliquotaICMS);

            icmsST    = result.ValorICMSST;
            baseCalcST = result.BaseCalculoST;
        }

        // PIS e COFINS (Simples Nacional — regime não-cumulativo zerado para varejistas)
        var pis    = produto.RegimePis == "99" ? 0m
            : Math.Round(baseCalc * (produto.AliquotaPIS / 100m), 2);
        var cofins = produto.RegimeCofins == "99" ? 0m
            : Math.Round(baseCalc * (produto.AliquotaCOFINS / 100m), 2);

        // IPI
        var ipi = Math.Round(baseCalc * (produto.AliquotaIPI / 100m), 2);

        return new ResultadoFiscal(
            baseCalc, icms, icmsST, baseCalcST, ipi, pis, cofins,
            Math.Round(baseCalc + icmsST + ipi, 2),
            aliqInterna);
    }

    // ── CFOP automático por tipo de operação ──────────────────────────────────

    public static string ObterCFOP(string ufOrigem, string ufDestino, bool ehServico = false)
    {
        if (ehServico) return ufOrigem == ufDestino ? "5933" : "6933";

        return (ufOrigem == ufDestino) switch
        {
            true  => "5102", // Venda de mercadoria adquirida de terceiro — mesmo estado
            false => "6102"  // Venda de mercadoria adquirida de terceiro — outro estado
        };
    }

    // ── IBPT — Carga tributária aproximada (Lei 12.741) ───────────────────────

    private static readonly Dictionary<string, (decimal Nacional, decimal Importado)> _ibpt = new()
    {
        // NCMs comuns em materiais de construção
        { "25232910", (24.51m, 43.68m) }, // Cimento
        { "38244090", (21.07m, 36.45m) }, // Argamassa
        { "25222000", (18.32m, 35.20m) }, // Cal
        { "85444200", (31.48m, 52.30m) }, // Cabos elétricos
        { "85362000", (28.90m, 47.10m) }, // Disjuntores
        { "39172100", (27.15m, 44.20m) }, // Tubos PVC
    };

    public static decimal ObterCargaTributariaIBPT(string ncm, decimal valor, bool importado = false)
    {
        if (!_ibpt.TryGetValue(ncm, out var aliqIBPT)) return valor * 0.2051m; // 20.51% padrão

        var aliq = importado ? aliqIBPT.Importado : aliqIBPT.Nacional;
        return Math.Round(valor * aliq / 100m, 2);
    }
}

// ── DTOs do Motor Fiscal ──────────────────────────────────────────────────────

public class ProdutoFiscal
{
    public decimal ValorProduto    { get; set; }
    public string  CSOSN           { get; set; } = "400";
    public decimal AliquotaICMS    { get; set; } = 0m;
    public bool    TemST           { get; set; } = false;
    public decimal? MVA            { get; set; }
    public string  UfOrigem        { get; set; } = "PR";
    public decimal AliquotaIPI     { get; set; } = 0m;
    public decimal AliquotaPIS     { get; set; } = 0.0065m;
    public decimal AliquotaCOFINS  { get; set; } = 0.03m;
    public string  RegimePis       { get; set; } = "07"; // 07=Isento Simples
    public string  RegimeCofins    { get; set; } = "07";
    public string? NCM             { get; set; }
}

public record ResultadoFiscal(
    decimal BaseCalculo,
    decimal ICMS,
    decimal ICMSST,
    decimal BaseCalculoST,
    decimal IPI,
    decimal PIS,
    decimal COFINS,
    decimal TotalComImpostos,
    decimal AliquotaInternaUF);