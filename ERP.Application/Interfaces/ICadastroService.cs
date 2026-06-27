using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface ICadastroService
{
    Task<CadastroResponseDto> CadastrarTenantAsync(CadastroRequestDto dto);
}
