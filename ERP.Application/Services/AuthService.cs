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
    // S13: constantes movidas para LockoutPolicy (ERP.Application/Helpers/LockoutPolicy.cs)
    // private const int MaxTentativas   = 5;
    // private const int MinutosBloqueio = 15;

    public AuthService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, Guid tenantId)
    {
        // Valida credencial DENTRO do tenant reivindicado no header X-Tenant-CNPJ.
        // GetByUsernameAndTenantAsync filtra por username + tenantId explicitamente,
        // impedindo que credencial de outro tenant seja aceita aqui (cross-tenant auth).
        var user = await _userRepository.GetByUsernameAndTenantAsync(dto.Username, tenantId);

        // Resposta genérica para não revelar se usuário existe
        if (user == null)
        {
            Log.Warning("Login inexistente: '{Username}'", dto.Username);
            return LoginResultDto.Falhou("Usuário ou senha incorretos.");
        }

        // Conta bloqueada?
        if (ERP.Application.Helpers.LockoutPolicy.EstaBloqueada(user.LockoutEndUtc))
        {
            var min = ERP.Application.Helpers.LockoutPolicy.MinutosRestantes(user.LockoutEndUtc!.Value);
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
            var (n, lockout) = ERP.Application.Helpers.LockoutPolicy.Calcular(user.FailedLoginAttempts);

            await _userRepository.UpdateLoginAttemptAsync(user.Id, tenantId, n, lockout);

            Log.Warning("Senha errada: '{Username}'. Tentativa {N}/{Max}.",
                dto.Username, n, ERP.Application.Helpers.LockoutPolicy.MaxTentativas);

            return LoginResultDto.Falhou(
                ERP.Application.Helpers.LockoutPolicy.MensagemErro(n, lockout));
        }

        // Sucesso — reset contador
        if (user.FailedLoginAttempts > 0 || user.LockoutEndUtc.HasValue)
            await _userRepository.UpdateLoginAttemptAsync(user.Id, tenantId, 0, null);

        Log.Information("Login OK: '{Username}' ({Nome}){Flag}",
            user.Username, user.Name,
            user.MustChangePassword ? " [MustChangePassword]" : "");

        return LoginResultDto.Sucesso(new UserDto
        {
            Id                    = user.Id,
            Name                  = user.Name,
            Username              = user.Username,
            RoleName              = user.Role?.Name ?? "Sem Perfil",
            Permissions           = user.Role?.Permissions.Select(p => p.Code).ToList() ?? new List<string>(),
            MaxDiscountPercentage = user.Role?.MaxDiscountPercentage ?? 0m,
            MaxSangriaValue       = user.Role?.MaxSangriaValue ?? 0m
        }, mustChangePassword: user.MustChangePassword);
    }

    /// <summary>
    /// Troca a senha do usuário autenticado (1.7.4 — MustChangePassword enforcement).
    /// Valida a senha atual antes de aceitar a nova.
    /// Ao concluir, seta MustChangePassword = false no banco.
    /// </summary>
    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new InvalidOperationException("Senha atual incorreta.");

        // S12 FIX: usa PasswordPolicy centralizado (antes: 8 chars sem dígito obrigatório)
        var (ok, erro) = ERP.Application.Helpers.PasswordPolicy.Validar(newPassword);
        if (!ok) throw new InvalidOperationException(erro!);

        if (newPassword == currentPassword)
            throw new InvalidOperationException("A nova senha deve ser diferente da senha atual.");

        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, 12);

        // S12 FIX: passa user.TenantId explicitamente (S10 N1 pattern)
        await _userRepository.UpdatePasswordAsync(userId, user.TenantId, hash, mustChangePassword: false);

        Log.Information("Senha alterada com sucesso para usuário {UserId}", userId);
    }

    public async Task EnsureDefaultAdminCreatedAsync()
    {
        if (!await _userRepository.HasAnyAsync())
        {
            await _userRepository.AddAsync(new User
            {
                Name               = "Administrador",
                Username           = "admin",
                PasswordHash       = BCrypt.Net.BCrypt.HashPassword("admin123", 12),
                IsActive           = true,
                MustChangePassword = true  // 1.6.8: força troca de senha no primeiro login
            });
        }
    }
}