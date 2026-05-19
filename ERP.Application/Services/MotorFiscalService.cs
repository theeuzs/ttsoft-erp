using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;

namespace ERP.Application.Services;

public class MotorFiscalService : IMotorFiscalService
{
    public ResultadoTributarioDto CalcularTributosVenda(Product produto, decimal quantidade, decimal valorUnitario, decimal desconto = 0, decimal frete = 0)
    {
        var resultado = new ResultadoTributarioDto();

        // 1. Valores Básicos
        resultado.ValorProduto = quantidade * valorUnitario;
        resultado.ValorFrete = frete;
        resultado.ValorDesconto = desconto;

        // O valor base para quase todos os impostos (Produto + Frete - Desconto)
        decimal valorBaseBruto = resultado.ValorProduto + resultado.ValorFrete - resultado.ValorDesconto;

        // ==========================================
        // 2. CÁLCULO DO IPI (Imposto de Fábrica)
        // O IPI incide sobre o valor bruto.
        // ==========================================
        resultado.AliquotaIpi = produto.IpiPercent;
        resultado.BaseCalculoIpi = valorBaseBruto;
        resultado.ValorIpi = resultado.BaseCalculoIpi * (resultado.AliquotaIpi / 100);

        // ==========================================
        // 3. CÁLCULO DO ICMS NORMAL
        // O IPI não entra na base do ICMS se for revenda, mas entra se for uso e consumo. 
        // Assumindo Regra Geral de Revenda:
        // ==========================================
        resultado.AliquotaIcms = produto.IcmsPercent;
        resultado.BaseCalculoIcms = valorBaseBruto;
        resultado.ValorIcms = resultado.BaseCalculoIcms * (resultado.AliquotaIcms / 100);

        // ==========================================
        // 4. CÁLCULO DO ICMS-ST (Substituição Tributária)
        // Essa é a matemática mais chata do Brasil!
        // ==========================================
        // Para a Fase 2 inicial, deixamos a MVA zerada até cadastrarmos as regras fiscais de estado para estado.
        resultado.MargemValorAgregado = 0; 
        
        if (resultado.MargemValorAgregado > 0)
        {
            // Base ST = (Valor Produto + IPI + Frete) + MVA%
            resultado.BaseCalculoIcmsSt = (valorBaseBruto + resultado.ValorIpi) * (1 + (resultado.MargemValorAgregado / 100));
            
            // Valor ST = (Base ST * Alíquota Interna) - ICMS Próprio (Normal)
            decimal valorStBruto = resultado.BaseCalculoIcmsSt * (resultado.AliquotaIcms / 100);
            resultado.ValorIcmsSt = valorStBruto - resultado.ValorIcms;
            if (resultado.ValorIcmsSt < 0) resultado.ValorIcmsSt = 0;
        }

        // ==========================================
        // 5. TOTAL DA NOTA/ITEM
        // O IPI e o ICMS-ST somam no total que o cliente paga! O ICMS normal já está embutido no preço.
        // ==========================================
        resultado.ValorTotalItem = valorBaseBruto + resultado.ValorIpi + resultado.ValorIcmsSt + resultado.ValorIcms;

        return resultado;
    }

    public ResultadoTributarioDto CalcularTributosCompra(decimal valorProduto, decimal aliquotaIpi, decimal aliquotaIcms, decimal mva, decimal aliquotaInternaSt)
    {
        // Aqui nós faremos a matemática reversa para o Módulo de Compras (Importação de XML) no futuro.
        // Por enquanto, retorna vazio para não dar erro de compilação.
        return new ResultadoTributarioDto();
    }
}