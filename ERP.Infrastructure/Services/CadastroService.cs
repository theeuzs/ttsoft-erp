using BCrypt.Net;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Fase 1 do onboarding self-service:
/// 1. Valida CNPJ (formato + unicidade)
/// 2. Deriva TenantId via SHA-256(CNPJ) — mesmo algoritmo do WPF
/// 3. Cria Roles padrão + Permissions padrão para o novo tenant
/// 4. Cria usuário admin com MustChangePassword = true
/// 5. Envia e-mail de boas-vindas via Zoho SMTP
/// </summary>
public class CadastroService : ICadastroService
{
    private readonly IUnitOfWork    _uow;
    private readonly AppDbContext   _db;
    private readonly IConfiguration _config;

    public CadastroService(IUnitOfWork uow, AppDbContext db, IConfiguration config)
    {
        _uow    = uow;
        _db     = db;
        _config = config;
    }

    public async Task<CadastroResponseDto> CadastrarTenantAsync(CadastroRequestDto dto)
    {
        // 1. Limpa e valida CNPJ
        var cnpjLimpo = new string(dto.Cnpj.Where(char.IsDigit).ToArray());
        if (cnpjLimpo.Length != 14)
            throw new InvalidOperationException("CNPJ inválido. Informe os 14 dígitos.");

        if (!ValidarCnpj(cnpjLimpo))
            throw new InvalidOperationException("CNPJ inválido. Verifique os dígitos informados.");

        // 2. Deriva TenantId — mesmo algoritmo SHA-256 do WPF/TenantHelper
        var tenantId = CnpjParaTenantId(cnpjLimpo);

        // 3. Verifica se tenant já existe
        var tenantExistente = await _uow.Users.GetByUsernameAndTenantAsync("admin", tenantId);
        if (tenantExistente != null)
            throw new InvalidOperationException("Este CNPJ já possui um cadastro ativo. Acesse o portal ou recupere sua senha.");

        // 4. Cria Roles padrão via DbContext (IRoleRepository.CreateAsync tem assinatura diferente)
        var roleAdmin    = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Administrador",  PercentualComissao = 0 };
        var roleVendedor = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Vendedor",       PercentualComissao = 0 };
        var roleCaixa    = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Operador Caixa", PercentualComissao = 0 };
        var roleGerente  = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Gerente",        PercentualComissao = 0 };

        _db.Roles.AddRange(roleAdmin, roleVendedor, roleCaixa, roleGerente);

        // 5. Cria usuário admin
        var senhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha);
        var admin = new User
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            Name               = dto.RazaoSocial,
            Username           = "admin",
            PasswordHash       = senhaHash,
            IsActive           = true,
            MustChangePassword = false,
            RoleId             = roleAdmin.Id
        };

        await _uow.Users.AddAsync(admin);
        await _uow.CommitAsync();

        Log.Information("Onboarding: novo tenant criado — CNPJ={Cnpj} TenantId={TenantId} RazaoSocial={RazaoSocial}",
            cnpjLimpo, tenantId, dto.RazaoSocial);

        // 6. Envia e-mail de boas-vindas (sem bloquear — falha é logada, não propagada)
        _ = EnviarEmailBoasVindasAsync(dto.Email, dto.RazaoSocial, dto.Cnpj, dto.Senha);

        return new CadastroResponseDto
        {
            MensagemSucesso = $"Cadastro realizado com sucesso! Acesse o portal com CNPJ {dto.Cnpj} e usuário admin.",
            LoginUrl        = "https://app.ttsofts.com.br"
        };
    }

    // ── Deriva TenantId via SHA-256(CNPJ) — mesmo algoritmo do WPF ─────────
    private static Guid CnpjParaTenantId(string cnpjLimpo)
    {
        using var sha = SHA256.Create();
        var hash      = sha.ComputeHash(Encoding.UTF8.GetBytes(cnpjLimpo));
        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }

    // ── Validação de CNPJ (dígitos verificadores) ────────────────────────
    private static bool ValidarCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1) return false; // 00000000000000 inválido

        int[] mult1 = { 5,4,3,2,9,8,7,6,5,4,3,2 };
        int[] mult2 = { 6,5,4,3,2,9,8,7,6,5,4,3,2 };

        var tempCnpj = cnpj[..12];
        var soma     = tempCnpj.Select((c, i) => (c - '0') * mult1[i]).Sum();
        var resto    = soma % 11;
        var d1       = resto < 2 ? 0 : 11 - resto;

        tempCnpj = tempCnpj + d1;
        soma     = tempCnpj.Select((c, i) => (c - '0') * mult2[i]).Sum();
        resto    = soma % 11;
        var d2   = resto < 2 ? 0 : 11 - resto;

        return cnpj.EndsWith($"{d1}{d2}");
    }

    // ── E-mail de boas-vindas via Zoho SMTP ──────────────────────────────
    private async Task EnviarEmailBoasVindasAsync(string emailDestino, string razaoSocial, string cnpj, string senha)
    {
        try
        {
            var smtpHost  = _config["Email:SmtpHost"]    ?? "smtppro.zoho.com";
            var smtpPort  = int.Parse(_config["Email:SmtpPort"] ?? "465");
            var smtpUser  = _config["Email:Usuario"]     ?? "";
            var smtpSenha = _config["Email:Senha"]       ?? "";

            if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpSenha))
            {
                Log.Warning("Onboarding: e-mail não enviado — credenciais SMTP não configuradas.");
                return;
            }

            var corpo = $@"
<!DOCTYPE html>
<html>
<body style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px"">
  <div style=""background:#1a56db;padding:24px;border-radius:8px 8px 0 0;text-align:center"">
    <h1 style=""color:white;margin:0"">TTSoft ERP</h1>
    <p style=""color:#93c5fd;margin:8px 0 0"">Bem-vindo ao sistema!</p>
  </div>
  <div style=""background:#f8fafc;padding:24px;border:1px solid #e2e8f0;border-radius:0 0 8px 8px"">
    <p>Olá, <strong>{razaoSocial}</strong>!</p>
    <p>Seu cadastro no TTSoft ERP foi realizado com sucesso. Aqui estão seus dados de acesso:</p>

    <div style=""background:white;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin:16px 0"">
      <p style=""margin:0 0 8px""><strong>🌐 Portal:</strong> <a href=""https://app.ttsofts.com.br"">app.ttsofts.com.br</a></p>
      <p style=""margin:0 0 8px""><strong>🏢 CNPJ:</strong> {cnpj}</p>
      <p style=""margin:0 0 8px""><strong>👤 Usuário:</strong> admin</p>
      <p style=""margin:0""><strong>🔑 Senha:</strong> {senha}</p>
    </div>

    <p style=""color:#ef4444"">⚠️ Por segurança, altere sua senha no primeiro acesso.</p>

    <div style=""text-align:center;margin:24px 0"">
      <a href=""https://app.ttsofts.com.br""
         style=""background:#1a56db;color:white;padding:12px 32px;border-radius:6px;text-decoration:none;font-weight:bold"">
        Acessar o Portal →
      </a>
    </div>

    <hr style=""border:none;border-top:1px solid #e2e8f0;margin:24px 0"">
    <p style=""color:#64748b;font-size:12px;text-align:center"">
      TTSoft ERP — Sistema especializado para materiais de construção<br>
      Dúvidas? Fale pelo WhatsApp ou responda este e-mail.
    </p>
  </div>
</body>
</html>";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl   = true,
                Credentials = new NetworkCredential(smtpUser, smtpSenha)
            };

            using var msg = new MailMessage
            {
                From       = new MailAddress(smtpUser, "TTSoft ERP"),
                Subject    = "✅ Seu acesso ao TTSoft ERP está pronto!",
                Body       = corpo,
                IsBodyHtml = true
            };
            msg.To.Add(emailDestino);

            await client.SendMailAsync(msg);
            Log.Information("Onboarding: e-mail de boas-vindas enviado para {Email}", emailDestino);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Onboarding: falha ao enviar e-mail de boas-vindas para {Email}", emailDestino);
        }
    }
}