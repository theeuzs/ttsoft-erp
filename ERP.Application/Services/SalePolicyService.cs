using ERP.Application.Interfaces;
using ERP.Domain.Enums;

namespace ERP.Application.Services;

public class SalePolicyService : ISalePolicyService
{
    public bool RequerCaixaAberto(SaleOrigin origem) => origem switch
    {
        SaleOrigin.PDV         => true,
        SaleOrigin.Marketplace => false,
        _                      => true // default seguro: exige caixa se a origem for desconhecida
    };
}
