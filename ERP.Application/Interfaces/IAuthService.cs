using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginDto dto, Guid tenantId);
    Task EnsureDefaultAdminCreatedAsync();
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
