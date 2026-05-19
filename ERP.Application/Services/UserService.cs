using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _repository.GetAllAsync();
        return users.Select(u => new UserDto 
        {
            Id = u.Id, 
            Name = u.Name, 
            Username = u.Username,
            RoleName = u.Role?.Name ?? "Vendedor",
            Permissions = u.Role?.Permissions.Select(p => p.Code).ToList() ?? new List<string>(),
            MaxDiscountPercentage = u.Role?.MaxDiscountPercentage ?? 0m
        });
    }

    public async Task CreateAsync(CreateUserDto dto)
{
    var user = new User
{
    Name = dto.Name,
    Username = dto.Username,
    // Mantendo a segurança com BCrypt que você já implementou
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 12),
    RoleId = dto.RoleId, // Vincula ao novo perfil
    IsActive = true
};

    await _repository.AddAsync(user);
}

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<IEnumerable<ERP.Domain.Entities.Role>> GetRolesAsync()
    {
        var users = await _repository.GetAllAsync();
        // Usa IUnitOfWork via repository para buscar roles distintas
        // Simplificação: extrai roles dos usuários carregados
        return users
            .Where(u => u.Role != null)
            .Select(u => u.Role!)
            .DistinctBy(r => r.Id)
            .OrderBy(r => r.Name)
            .ToList();
    }
}