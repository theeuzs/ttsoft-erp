namespace ERP.Domain.Services.Fiscal;

/// <summary>
/// Resultado do cálculo de ICMS-ST para um item.
/// </summary>
public record ICMSSTResult(
    decimal BaseCalculoST,
    decimal ValorICMSST,
    decimal MVAUtilizado,
    bool    IsInterestadual);

/// <summary>
/// Calcula ICMS-ST (Substituição Tributária) para o Simples Nacional.
/// Suporta operações internas e interestaduais com MVA ajustado.
/// Referência: Convênio ICMS 142/2018.
/// </summary>
public class ICMSSTCalculator
{
    /// <summary>
    /// Calcula o ICMS-ST para uma operação.
    /// </summary>
    /// <param name="valorProduto">Valor do produto sem impostos</param>
    /// <param name="aliquotaInternaUFDest">Alíquota interna do estado destino (%)</param>
    /// <param name="mvaOriginal">MVA original do produto (%)</param>
    /// <param name="aliquotaInterestadual">Alíquota interestadual. 0 = operação interna</param>
    /// <param name="aliquotaIcmsOrigem">Alíquota ICMS origem (apenas Simples: 0 para não-contribuinte)</param>
    public ICMSSTResult Calcular(
        decimal valorProduto,
        decimal aliquotaInternaUFDest,
        decimal mvaOriginal,
        decimal aliquotaInterestadual = 0m,
        decimal aliquotaIcmsOrigem   = 0m)
    {
        bool isInterestadual = aliquotaInterestadual > 0;

        // MVA ajustado para operações interestaduais (Protocolo ICMS 41/2008)
        // Formula: MVA_Aj = [(1 + MVA_ST Original) × (1 - ALQ_inter) / (1 - ALQ_intra)] - 1
        decimal mvaUtilizado;
        if (isInterestadual)
        {
            var fatorInter  = 1m - (aliquotaInterestadual / 100m);
            var fatorIntra  = 1m - (aliquotaInternaUFDest / 100m);
            mvaUtilizado    = fatorIntra > 0
                ? ((1m + mvaOriginal / 100m) * fatorInter / fatorIntra - 1m) * 100m
                : mvaOriginal;
        }
        else
        {
            mvaUtilizado = mvaOriginal;
        }

        // Base de cálculo ST = Valor do produto × (1 + MVA/100)
        var baseCalculo = valorProduto * (1m + mvaUtilizado / 100m);

        // Valor ICMS-ST = (BC_ST × Alíquota_interna) - ICMS_operação_própria
        var icmsProprioPercent = isInterestadual ? aliquotaInterestadual : aliquotaIcmsOrigem;
        var icmsProprio        = valorProduto * (icmsProprioPercent / 100m);
        var valorST            = Math.Max(0, baseCalculo * (aliquotaInternaUFDest / 100m) - icmsProprio);

        return new ICMSSTResult(
            Math.Round(baseCalculo, 2),
            Math.Round(valorST, 2),
            Math.Round(mvaUtilizado, 4),
            isInterestadual);
    }

    /// <summary>Calcula ST diretamente de um produto.</summary>
    public ICMSSTResult? CalcularDoProduto(
        ERP.Domain.Entities.Product produto,
        decimal valorVenda,
        decimal aliquotaInterestadual = 0m)
    {
        if (!produto.TemSubstituicaoTrib) return null;
        if (produto.AliquotaInternaUFDest is null || produto.MVAOriginal is null) return null;

        return Calcular(
            valorVenda,
            produto.AliquotaInternaUFDest.Value,
            produto.MVAOriginal.Value,
            aliquotaInterestadual,
            produto.IcmsPercent);
    }
}
