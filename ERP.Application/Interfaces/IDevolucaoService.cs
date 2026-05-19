using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IDevolucaoService
{
    Task<DevolucaoResultDto> DevolverItensAsync(CreateDevolucaoDto dto);
    Task<decimal> GetQuantidadeJaDevolvida(Guid saleId, Guid productId);
}
