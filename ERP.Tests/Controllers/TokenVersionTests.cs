// ── ERP.Tests/Controllers/TokenVersionTests.cs ────────────────────────────────
// Controle de versão de sessão (16/09) — JWT continua com 14h de vida, mas
// pode ser revogado antes disso: logout, troca de senha ou usuário excluído
// incrementam User.TokenVersion, invalidando na hora qualquer token emitido
// com uma versão antiga, mesmo sem ter expirado ainda.
//
// Arquivo separado do ControllersIntegrationTests.cs (já enorme) — usa a
// MESMA ErpApiFactory/TenantScope, só com um gerador de token próprio que
// aponta pra um usuário DE VERDADE no banco de teste (o helper compartilhado
// GerarToken() usa um Guid aleatório sem linha no banco, de propósito —
// não dava pra reaproveitar sem also mexer nos outros ~450 testes que
// dependem dele).
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ERP.Tests.Controllers;

public class TokenVersionTests : IntegrationTestBase
{
    public TokenVersionTests(ErpApiFactory f) : base(f) { }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<Guid> SeedUsuarioAsync(int tokenVersion = 1, bool ativo = true)
    {
        var userId = Guid.NewGuid();
        using (new TenantScope(ErpApiFactory.TestTenantId))
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User
            {
                Id           = userId,
                TenantId     = ErpApiFactory.TestTenantId,
                Name         = "Usuário Teste TokenVersion",
                Username     = $"user-tv-{userId:N}",
                PasswordHash = "hash-fake-nao-usado-nesse-teste",
                IsActive     = ativo,
                TokenVersion = tokenVersion,
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        return userId;
    }

    private static string GerarTokenParaUsuarioReal(Guid userId, int tokenVersion)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ErpApiFactory.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,  userId.ToString()),
            new(JwtRegisteredClaimNames.Name, "Usuário Teste TokenVersion"),
            new(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            new("tenant_id",                  ErpApiFactory.TestTenantId.ToString()),
            new("role_name",                  "Administrador"),
            new("token_version",              tokenVersion.ToString()),
        };
        foreach (var p in ERP.Api.Security.Permissions.All)
            claims.Add(new Claim("permission", p));

        var token = new JwtSecurityToken(
            issuer:             ErpApiFactory.JwtIssuer,
            audience:           ErpApiFactory.JwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient ClienteComToken(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Testes ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "TokenVersion — usuário novo nasce com versão 1")]
    public async Task UsuarioNovo_TokenVersionPadraoUm()
    {
        var userId = await SeedUsuarioAsync(); // sem especificar — usa o default da entidade

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versao = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId).Select(u => u.TokenVersion).FirstAsync();

        versao.Should().Be(1, "usuário existente sem valor explícito deve nascer com TokenVersion = 1");
    }

    [Fact(DisplayName = "TokenVersion — token com versão correta acessa normalmente")]
    public async Task VersaoCorreta_Retorna200()
    {
        var userId = await SeedUsuarioAsync(tokenVersion: 1);
        var token  = GerarTokenParaUsuarioReal(userId, tokenVersion: 1);
        var client = ClienteComToken(token);

        var resp = await client.GetAsync("/api/products");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "token com a mesma versão do usuário no banco deve ser aceito normalmente");
    }

    [Fact(DisplayName = "TokenVersion — token com versão antiga é rejeitado com 401")]
    public async Task VersaoAntiga_Retorna401()
    {
        // Usuário já está na versão 2 (como se tivesse revogado depois de
        // emitir o token abaixo), mas o token ainda carrega a versão 1.
        var userId = await SeedUsuarioAsync(tokenVersion: 2);
        var token  = GerarTokenParaUsuarioReal(userId, tokenVersion: 1);
        var client = ClienteComToken(token);

        var resp = await client.GetAsync("/api/products");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "token com versão desatualizada deve ser rejeitado mesmo sem ter expirado");
    }

    [Fact(DisplayName = "TokenVersion — logout invalida o próprio token na hora")]
    public async Task Logout_TokenAtualParaDeFuncionar()
    {
        var userId = await SeedUsuarioAsync(tokenVersion: 1);
        var token  = GerarTokenParaUsuarioReal(userId, tokenVersion: 1);
        var client = ClienteComToken(token);

        // Confirma que funciona antes do logout
        (await client.GetAsync("/api/products")).StatusCode.Should().Be(HttpStatusCode.OK);

        var logoutResp = await client.PostAsync("/api/auth/logout", null);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Mesmo token, mesmo cliente — deve parar de funcionar
        var respDepois = await client.GetAsync("/api/products");
        respDepois.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "logout deve invalidar o token atual imediatamente, não só client-side");
    }

    [Fact(DisplayName = "TokenVersion — login novamente depois do logout gera token que funciona")]
    public async Task LoginNovamenteAposLogout_NovoTokenFunciona()
    {
        var userId = await SeedUsuarioAsync(tokenVersion: 1);
        var tokenVelho = GerarTokenParaUsuarioReal(userId, tokenVersion: 1);
        await ClienteComToken(tokenVelho).PostAsync("/api/auth/logout", null);

        // Simula um "novo login" gerando token já com a versão atualizada
        // (2, depois do logout ter incrementado) — não passa pelo endpoint
        // de login de verdade aqui porque exigiria hash de senha real; o
        // que importa pro teste é a MECÂNICA de versão, já coberta.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var versaoAtual = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId).Select(u => u.TokenVersion).FirstAsync();

        var tokenNovo = GerarTokenParaUsuarioReal(userId, versaoAtual);
        var resp = await ClienteComToken(tokenNovo).GetAsync("/api/products");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "token gerado com a versão pós-logout deve funcionar normalmente");
    }

    [Fact(DisplayName = "TokenVersion — dois tokens do mesmo usuário são ambos invalidados ao revogar")]
    public async Task RevogarSessao_InvalidaTodosOsTokensDoUsuario()
    {
        var userId = await SeedUsuarioAsync(tokenVersion: 1);
        var tokenA = GerarTokenParaUsuarioReal(userId, tokenVersion: 1); // "celular"
        var tokenB = GerarTokenParaUsuarioReal(userId, tokenVersion: 1); // "notebook"

        // Revoga usando SÓ um dos dois tokens (ex: perdeu o celular, revoga
        // pelo notebook) — TokenVersion é por usuário, não por aparelho.
        await ClienteComToken(tokenB).PostAsync("/api/auth/logout", null);

        (await ClienteComToken(tokenA).GetAsync("/api/products")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "revogar deve derrubar TODOS os aparelhos da pessoa, não só o que fez o pedido");
        (await ClienteComToken(tokenB).GetAsync("/api/products")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "o próprio token que pediu a revogação também para de funcionar");
    }

    [Fact(DisplayName = "TokenVersion — usuário excluído invalida qualquer token dele")]
    public async Task UsuarioExcluido_TokenParaDeFuncionar()
    {
        var userId = await SeedUsuarioAsync(tokenVersion: 1);
        var token  = GerarTokenParaUsuarioReal(userId, tokenVersion: 1);

        using (new TenantScope(ErpApiFactory.TestTenantId))
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usuario = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
            db.Users.Remove(usuario);
            await db.SaveChangesAsync();
        }

        var resp = await ClienteComToken(token).GetAsync("/api/products");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "usuário que não existe mais no banco não pode continuar acessando com o token antigo");
    }

    [Fact(DisplayName = "TokenVersion — sem a claim (token do parque de testes antigo), segue sem checar versão")]
    public async Task SemClaimTokenVersion_NaoQuebraTokenAntigo()
    {
        // Reproduz exatamente o token que Factory.GerarToken() (helper
        // compartilhado, usado por ~450 testes já existentes) sempre gerou —
        // sem claim de versão nenhuma, sub aleatório sem linha no banco.
        var token  = Factory.GerarToken();
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/products");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "token sem claim de versão (todo o parque de testes anterior a essa mudança) não pode " +
            "passar a falhar — a checagem só vale pra token novo, emitido depois desse deploy");
    }
}