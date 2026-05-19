using ERP.Application.DTOs;
using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

public interface IMotorFiscalService
{
    // O cálculo principal de saída (Venda no PDV)
    ResultadoTributarioDto CalcularTributosVenda(Product produto, decimal quantidade, decimal valorUnitario, decimal desconto = 0, decimal frete = 0);
    
    // O cálculo de entrada (Quando você compra mercadoria e precisa saber o custo real)
    ResultadoTributarioDto CalcularTributosCompra(decimal valorProduto, decimal aliquotaIpi, decimal aliquotaIcms, decimal mva, decimal aliquotaInternaSt);
}