using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

/// <summary>
/// Decide regras operacionais por origem de venda — hoje só "exige caixa
/// aberto?", mas é o lugar certo pra crescer (representante, API própria)
/// sem o SaleService precisar saber de cada canal individualmente.
/// </summary>
public interface ISalePolicyService
{
    /// <summary>PDV físico exige; Marketplace (e qualquer canal futuro sem operador) não.</summary>
    bool RequerCaixaAberto(SaleOrigin origem);
}
