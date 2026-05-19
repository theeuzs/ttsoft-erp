using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginDto dto);
    Task EnsureDefaultAdminCreatedAsync();
}
