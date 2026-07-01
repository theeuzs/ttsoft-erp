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
    private readonly IUnitOfWork       _uow;
    private readonly AppDbContext      _db;
    private readonly IConfiguration   _config;
    private readonly BrasilApiService  _brasilApi;

    public CadastroService(IUnitOfWork uow, AppDbContext db, IConfiguration config, BrasilApiService brasilApi)
    {
        _uow       = uow;
        _db        = db;
        _config    = config;
        _brasilApi = brasilApi;
    }

    public async Task<CadastroResponseDto> CadastrarTenantAsync(CadastroRequestDto dto)
    {
        // 1. Limpa e valida CNPJ
        var cnpjLimpo = new string(dto.Cnpj.Where(char.IsDigit).ToArray());
        if (cnpjLimpo.Length != 14)
            throw new InvalidOperationException("CNPJ inválido. Informe os 14 dígitos.");

        if (!ValidarCnpj(cnpjLimpo))
            throw new InvalidOperationException("CNPJ inválido. Verifique os dígitos informados.");

        // 1b. S11 — Validação Receita Federal via BrasilAPI (fail-open):
        //   Nível 1: rejeita CNPJs inativos/baixados/suspensos.
        //   Nível 2 S12: cria admin inativo e envia confirmação para e-mail RFB quando há divergência.
        bool   divergenciaEmailRfb    = false;
        string? emailRfbConfirmacao   = null;
        var dadosRfb = await _brasilApi.ConsultarCnpjAsync(cnpjLimpo);
        if (dadosRfb != null)
        {
            var situacao = dadosRfb.DescricaoSituacaoCadastral?.Trim().ToUpperInvariant();
            if (situacao != "ATIVA")
            {
                Log.Warning("Onboarding bloqueado: CNPJ {Cnpj} situação RFB = {Situacao}", cnpjLimpo, situacao);
                throw new InvalidOperationException(
                    $"Este CNPJ consta como \"{dadosRfb.DescricaoSituacaoCadastral}\" na Receita Federal. " +
                    "Apenas empresas com situação ATIVA podem se cadastrar.");
            }

            // S12 FIX: quando há divergência de e-mail, cria admin INATIVO e envia
            // token de confirmação para o e-mail da RFB — squatting bloqueado porque
            // atacante não tem acesso ao e-mail oficial da empresa.
            emailRfbConfirmacao = dadosRfb.Email?.Trim().ToLowerInvariant();
            var emailDto = dto.Email?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(emailRfbConfirmacao) &&
                !string.IsNullOrEmpty(emailDto) &&
                emailRfbConfirmacao != emailDto)
            {
                divergenciaEmailRfb = true;
                Log.Warning(
                    "Onboarding: divergência e-mail informado ({EmailDto}) vs RFB ({EmailRfb}) para CNPJ {Cnpj}. " +
                    "Conta criada INATIVA — confirmação enviada para e-mail RFB.",
                    emailDto, emailRfbConfirmacao, cnpjLimpo);
            }
        }

        // 2. Deriva TenantId — mesmo algoritmo SHA-256 do WPF/TenantHelper
        var tenantId = CnpjParaTenantId(cnpjLimpo);

        // 3. Verifica se tenant já existe — S11 FIX: exceção dedicada (não
        // InvalidOperationException genérica) para que o Controller possa
        // engolir e retornar 200 com mensagem idêntica ao caso de sucesso,
        // fechando o oráculo de enumeração via status code (S11 N3a).
        var tenantExistente = await _uow.Users.GetByUsernameAndTenantAsync("admin", tenantId);
        if (tenantExistente != null)
            throw new ERP.Application.Exceptions.TenantJaExisteException();

        // 4. Cria Permissions padrão (mesmo conjunto do DbSeeder)
        // S10 FIX: antes criava roles sem permissions — admin não conseguia fazer nada no Portal
        var allPerms = CriarPermissoesPadrao(tenantId);
        _db.Permissions.AddRange(allPerms);

        // 5. Cria Roles com permissões vinculadas
        var roleAdmin    = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Administrador",  PercentualComissao = 0, MaxDiscountPercentage = 100, MaxSangriaValue = 99999 };
        var roleVendedor = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Vendedor",       PercentualComissao = 0 };
        var roleCaixa    = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Operador Caixa", PercentualComissao = 0 };
        var roleGerente  = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Gerente",        PercentualComissao = 0, MaxDiscountPercentage = 50 };

        // Administrador recebe todas as permissões
        foreach (var p in allPerms) { roleAdmin.Permissions.Add(p); p.Roles.Add(roleAdmin); }

        _db.Roles.AddRange(roleAdmin, roleVendedor, roleCaixa, roleGerente);

        // 5. Cria usuário admin
        var senhaHash        = BCrypt.Net.BCrypt.HashPassword(dto.Senha);
        var confirmacaoToken = divergenciaEmailRfb ? Guid.NewGuid().ToString("N") : null;
        var admin = new User
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            Name               = dto.RazaoSocial,
            Username           = "admin",
            PasswordHash       = senhaHash,
            // S12 FIX: IsActive=false quando e-mail informado diverge do e-mail RFB.
            // Squatting bloqueado: atacante não tem acesso ao e-mail oficial da empresa
            // e não consegue confirmar o cadastro.
            IsActive           = !divergenciaEmailRfb,
            MustChangePassword = true,
            Email              = dto.Email,
            ConfirmacaoToken   = confirmacaoToken,
            RoleId             = roleAdmin.Id
        };

        await _uow.Users.AddAsync(admin);
        await _uow.CommitAsync();

        Log.Information("Onboarding: novo tenant criado — CNPJ={Cnpj} TenantId={TenantId} IsActive={IsActive}",
            cnpjLimpo, tenantId, admin.IsActive);

        if (divergenciaEmailRfb && confirmacaoToken != null)
        {
            // Envia confirmação para o e-mail da RFB — não o e-mail informado pelo atacante
            _ = EnviarEmailConfirmacaoAsync(emailRfbConfirmacao!, dto.RazaoSocial, confirmacaoToken);
            return new CadastroResponseDto
            {
                MensagemSucesso = "Cadastro recebido! Para ativar sua conta, verifique o e-mail " +
                                  "registrado na Receita Federal da sua empresa e clique no link de confirmação.",
                LoginUrl        = "https://app.ttsofts.com.br"
            };
        }

        // Fluxo normal: e-mails coincidem ou BrasilAPI indisponível (fail-open)
        _ = EnviarEmailBoasVindasAsync(dto.Email, dto.RazaoSocial, dto.Cnpj);

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

    // ── Cria permissões padrão (mesmo conjunto do DbSeeder) ──────────────────
    private static List<Permission> CriarPermissoesPadrao(Guid tenantId) =>
    [
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "sale.discount",      Description = "Conceder desconto em vendas" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "sale.cancel",        Description = "Cancelar vendas finalizadas" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "sale.return",        Description = "Devolução parcial de itens" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "cash.sangria",       Description = "Realizar sangria e suprimento" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "cash.view.summary",  Description = "Ver resumo do caixa" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "product.edit",       Description = "Criar e editar produtos" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "product.edit.price", Description = "Alterar preço de produtos" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "stock.adjust",       Description = "Ajuste de estoque" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "customers.edit",     Description = "Criar e editar clientes" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "customers.delete",   Description = "Excluir clientes" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "fidelidade.use",     Description = "Resgatar pontos de fidelidade" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "haver.edit",         Description = "Depositar e retirar Haver" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "entregas.manage",    Description = "Gerir entregas" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "orcamento.manage",   Description = "Converter orçamento em venda" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "report.financial",   Description = "Dashboard e indicadores" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "financeiro.view",    Description = "Contas a receber" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "despesas.view",      Description = "Contas a pagar" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "fluxocaixa.view",    Description = "Fluxo de Caixa e DRE" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "margem.view",        Description = "Tela de Margem" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "audit.view",         Description = "Auditoria" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "compras.view",       Description = "Módulo de Compras" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "inventario.view",    Description = "Inventário" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "notasfiscais.view",  Description = "Notas Fiscais" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "users.view",         Description = "Gestão de usuários" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "config.view",        Description = "Configurações do sistema" },
        new() { Id = Guid.NewGuid(), TenantId = tenantId, Code = "role.manage",        Description = "Criar e editar cargos" },
    ];
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
    // S12: E-mail de confirmação enviado ao e-mail RFB quando há divergência
    private async Task EnviarEmailConfirmacaoAsync(string emailRfb, string razaoSocial, string token)
    {
        try
        {
            var smtpHost  = _config["Email:SmtpHost"] ?? "smtppro.zoho.com";
            var smtpPort  = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var smtpUser  = _config["Email:Usuario"] ?? "";
            var smtpSenha = _config["Email:Senha"]   ?? "";

            if (string.IsNullOrWhiteSpace(smtpUser)) return;

            var link = $"https://app.ttsofts.com.br/confirmar-cadastro?token={Uri.EscapeDataString(token)}";

            var corpo = $@"
<!DOCTYPE html><html><body style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px'>
  <div style='background:#1a56db;padding:24px;border-radius:8px 8px 0 0;text-align:center'>
    <h1 style='color:white;margin:0'>TTSoft ERP</h1>
    <p style='color:#93c5fd;margin:8px 0 0'>Confirme o cadastro da sua empresa</p>
  </div>
  <div style='background:#f8fafc;padding:24px;border:1px solid #e2e8f0;border-radius:0 0 8px 8px'>
    <p>Olá! Recebemos uma solicitação de cadastro no TTSoft ERP para a empresa <strong>{razaoSocial}</strong>.</p>
    <p>Este e-mail é o registrado oficialmente na Receita Federal para este CNPJ.
       Para confirmar que você autoriza o cadastro, clique no botão abaixo:</p>
    <div style='text-align:center;margin:24px 0'>
      <a href='{link}' style='background:#1a56db;color:white;padding:12px 32px;border-radius:6px;text-decoration:none;font-weight:bold'>
        Confirmar Cadastro →
      </a>
    </div>
    <p style='color:#ef4444;font-size:13px'>
      ⚠️ Se você não solicitou este cadastro, ignore este e-mail.
      A conta permanecerá inativa e ninguém conseguirá acessar o sistema.
    </p>
    <hr style='border:none;border-top:1px solid #e2e8f0;margin:20px 0'>
    <p style='color:#64748b;font-size:12px;text-align:center'>
      TTSoft ERP — Sistema para materiais de construção
    </p>
  </div>
</body></html>";

            using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl   = true,
                Credentials = new System.Net.NetworkCredential(smtpUser, smtpSenha)
            };
            using var msg = new System.Net.Mail.MailMessage(
                smtpUser, emailRfb, "🏢 Confirme o cadastro da sua empresa — TTSoft ERP", corpo)
            {
                IsBodyHtml = true
            };
            await client.SendMailAsync(msg);
            Log.Information("Onboarding: e-mail de confirmação enviado para {EmailRfb}", emailRfb);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Onboarding: falha ao enviar e-mail de confirmação para {EmailRfb}", emailRfb);
        }
    }

    private async Task EnviarEmailBoasVindasAsync(string emailDestino, string razaoSocial, string cnpj)
    {
        try
        {
            var smtpHost  = _config["Email:SmtpHost"]    ?? "smtppro.zoho.com";
            var smtpPort  = int.Parse(_config["Email:SmtpPort"] ?? "587");
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
    <p>Seu cadastro no TTSoft ERP foi realizado com sucesso. Acesse o sistema com o CNPJ e a senha que você definiu no cadastro.</p>

    <div style=""background:white;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin:16px 0"">
      <p style=""margin:0 0 8px""><strong>🌐 Portal:</strong> <a href=""https://app.ttsofts.com.br"">app.ttsofts.com.br</a></p>
      <p style=""margin:0 0 8px""><strong>🏢 CNPJ:</strong> {cnpj}</p>
      <p style=""margin:0""><strong>👤 Usuário:</strong> admin</p>
    </div>

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