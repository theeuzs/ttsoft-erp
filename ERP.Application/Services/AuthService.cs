using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;

    // ── Política de bloqueio ────────────────────────────────────────────────
    private const int MaxTentativas   = 5;
    private const int MinutosBloqueio = 15;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto)
    {
        var user = await _userRepository.GetByUsernameAsync(dto.Username);

        // Resposta genérica para não revelar se usuário existe
        if (user == null)
        {
            Log.Warning("Login inexistente: '{Username}'", dto.Username);
            return LoginResultDto.Falhou("Usuário ou senha incorretos.");
        }

        // Conta bloqueada?
        if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc > DateTime.UtcNow)
        {
            var min = (int)Math.Ceiling((user.LockoutEndUtc.Value - DateTime.UtcNow).TotalMinutes);
            Log.Warning("Login bloqueado: '{Username}' por mais {Min} min.", dto.Username, min);
            return LoginResultDto.Falhou(
                $"Conta bloqueada por excesso de tentativas.\nAguarde {min} minuto(s) ou contate o administrador.");
        }

        // Conta inativa?
        if (!user.IsActive)
        {
            Log.Warning("Login em conta inativa: '{Username}'", dto.Username);
            return LoginResultDto.Falhou("Conta inativa. Contate o administrador.");
        }

        // Senha errada?
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            var n = user.FailedLoginAttempts + 1;
            DateTime? lockout = n >= MaxTentativas
                ? DateTime.UtcNow.AddMinutes(MinutosBloqueio)
                : null;

            await _userRepository.UpdateLoginAttemptAsync(user.Id, n, lockout);

            Log.Warning("Senha errada: '{Username}'. Tentativa {N}/{Max}.", dto.Username, n, MaxTentativas);

            var msg = lockout.HasValue
                ? $"Usuário ou senha incorretos. Conta bloqueada por {MinutosBloqueio} minutos."
                : $"Usuário ou senha incorretos. ({MaxTentativas - n} tentativa(s) restante(s))";

            return LoginResultDto.Falhou(msg);
        }

        // Sucesso — reset contador
        if (user.FailedLoginAttempts > 0 || user.LockoutEndUtc.HasValue)
            await _userRepository.UpdateLoginAttemptAsync(user.Id, 0, null);

        Log.Information("Login OK: '{Username}' ({Nome})", user.Username, user.Name);

        return LoginResultDto.Sucesso(new UserDto
        {
            Id                    = user.Id,
            Name                  = user.Name,
            Username              = user.Username,
            RoleName              = user.Role?.Name ?? "Sem Perfil",
            Permissions           = user.Role?.Permissions.Select(p => p.Code).ToList() ?? new List<string>(),
            MaxDiscountPercentage = user.Role?.MaxDiscountPercentage ?? 0m,
            MaxSangriaValue       = user.Role?.MaxSangriaValue ?? 0m
        });
    }

    public async Task EnsureDefaultAdminCreatedAsync()
    {
        if (!await _userRepository.HasAnyAsync())
        {
            await _userRepository.AddAsync(new User
            {
                Name         = "Administrador",
                Username     = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
                IsActive     = true
            });
        }
    }
}
