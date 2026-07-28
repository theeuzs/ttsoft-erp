// ── ERP.Application/Interfaces/IFiscalService.cs ────────────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Emissão fiscal sem nenhuma dependência de UI — reconstrói tudo que precisa
/// a partir da Venda já persistida (Sale/SaleItem/Product/Customer/SalePayment),
/// não de um carrinho em memória. É por isso que dá pra chamar tanto do PDV
/// (WPF) quanto do processamento de pedido de marketplace (API), com o mesmo
/// resultado.
/// </summary>
public interface IFiscalService
{
    /// <param name="tipoDocumento">"NFCE" ou "NFE" (A4).</param>
    Task<FiscalEmissionResult> EmitirNotaAsync(Guid vendaId, string tipoDocumento);
}
