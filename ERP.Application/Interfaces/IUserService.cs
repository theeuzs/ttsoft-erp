using ERP.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ERP.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task CreateAsync(CreateUserDto dto);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<ERP.Domain.Entities.Role>> GetRolesAsync();
}