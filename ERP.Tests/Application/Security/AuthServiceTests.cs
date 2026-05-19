using ERP.Application.DTOs;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application.Security
{
    [Trait("Categoria", "Autenticação")]
    public class AuthServiceTests
    {
        private readonly Mock<IUserRepository> _repoMock;
        private readonly AuthService           _authService;

        // Hash BCrypt de "Senha@123" — gerado uma vez, reutilizado nos testes
        private const string HashSenhaCorreta =
            "$2b$12$0WWQu20zk3bJEWfKDFgRR.zcc6pRbfkj4MLuSW7J2avFyxiVf.g/u";

        public AuthServiceTests()
        {
            _repoMock   = new Mock<IUserRepository>();
            _authService = new AuthService(_repoMock.Object);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private User CriarUsuarioAtivo(int tentativas = 0, DateTime? bloqueioAte = null) => new()
        {
            Id                   = Guid.NewGuid(),
            Name                 = "João Teste",
            Username             = "joao",
            PasswordHash         = HashSenhaCorreta,
            IsActive             = true,
            FailedLoginAttempts  = tentativas,
            LockoutEndUtc        = bloqueioAte,
            Role = new Role
            {
                Name                 = "Gerente",
                MaxDiscountPercentage = 30m,
                MaxSangriaValue      = 5000m,
                Permissions          = new List<Permission>
                {
                    new() { Code = "financeiro.view" },
                    new() { Code = "sale.cancel" }
                }
            }
        };

        // ── Cenários de sucesso ────────────────────────────────────────────────

        [Fact(DisplayName = "Login com credenciais corretas deve retornar sucesso com dados do usuário")]
        public async Task Login_CredenciaisCorretas_DeveRetornarSucesso()
        {
            var user = CriarUsuarioAtivo();
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(new LoginDto
                { Username = "joao", Password = "Senha@123" });

            result.Sucedeu.Should().BeTrue();
            result.Usuario.Should().NotBeNull();
            result.Usuario!.RoleName.Should().Be("Gerente");
            result.Usuario.MaxDiscountPercentage.Should().Be(30m);
            result.Usuario.Permissions.Should().Contain("financeiro.view");
        }

        [Fact(DisplayName = "Login bem-sucedido deve resetar contador de tentativas")]
        public async Task Login_BemSucedido_DeveResetarContadorDeTentativas()
        {
            // Usuário já tinha 3 tentativas falhas
            var user = CriarUsuarioAtivo(tentativas: 3);
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            await _authService.LoginAsync(new LoginDto
                { Username = "joao", Password = "Senha@123" });

            // Deve ter chamado reset (tentativas=0, bloqueio=null)
            _repoMock.Verify(r =>
                r.UpdateLoginAttemptAsync(user.Id, 0, null),
                Times.Once);
        }

        // ── Cenários de falha ─────────────────────────────────────────────────

        [Fact(DisplayName = "Login com usuário inexistente deve retornar mensagem genérica")]
        public async Task Login_UsuarioInexistente_DeveRetornarMensagemGenerica()
        {
            _repoMock.Setup(r => r.GetByUsernameAsync(It.IsAny<string>()))
                     .ReturnsAsync((User?)null);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "naoexiste", Password = "qualquer" });

            result.Sucedeu.Should().BeFalse();
            // Mensagem genérica — não revela se o usuário existe
            result.Mensagem.Should().Contain("incorretos");
        }

        [Fact(DisplayName = "Login com senha errada deve incrementar contador de tentativas")]
        public async Task Login_SenhaErrada_DeveIncrementarContador()
        {
            var user = CriarUsuarioAtivo(tentativas: 1);
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "joao", Password = "senhaErrada" });

            result.Sucedeu.Should().BeFalse();
            _repoMock.Verify(r =>
                r.UpdateLoginAttemptAsync(user.Id, 2, null),
                Times.Once);
        }

        [Fact(DisplayName = "Login com 5ª senha errada deve bloquear a conta")]
        public async Task Login_QuintaSenhaErrada_DeveBloquearConta()
        {
            var user = CriarUsuarioAtivo(tentativas: 4); // 4 falhas anteriores
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "joao", Password = "senhaErrada" });

            result.Sucedeu.Should().BeFalse();
            result.Mensagem.Should().Contain("bloqueada");

            // Deve ter registrado bloqueio com data futura
            _repoMock.Verify(r =>
                r.UpdateLoginAttemptAsync(
                    user.Id,
                    5,
                    It.Is<DateTime?>(d => d.HasValue && d.Value > DateTime.UtcNow)),
                Times.Once);
        }

        [Fact(DisplayName = "Login com conta bloqueada deve rejeitar mesmo com senha correta")]
        public async Task Login_ContaBloqueada_DeveRejeitarComSenhaCorreta()
        {
            var bloqueioAte = DateTime.UtcNow.AddMinutes(10);
            var user = CriarUsuarioAtivo(tentativas: 5, bloqueioAte: bloqueioAte);
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "joao", Password = "Senha@123" });

            result.Sucedeu.Should().BeFalse();
            result.Mensagem.Should().Contain("bloqueada");
            // Não deve ter tentado atualizar o contador (conta ainda bloqueada)
            _repoMock.Verify(r =>
                r.UpdateLoginAttemptAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<DateTime?>()),
                Times.Never);
        }

        [Fact(DisplayName = "Login com conta inativa deve rejeitar com mensagem específica")]
        public async Task Login_ContaInativa_DeveRejeitar()
        {
            var user = CriarUsuarioAtivo();
            user.IsActive = false;
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "joao", Password = "Senha@123" });

            result.Sucedeu.Should().BeFalse();
            result.Mensagem.Should().Contain("inativa");
        }

        [Fact(DisplayName = "Login com bloqueio expirado deve permitir acesso com senha correta")]
        public async Task Login_BloqueioExpirado_DevePermitirAcesso()
        {
            // Bloqueio que JÁ expirou (no passado)
            var bloqueioExpirado = DateTime.UtcNow.AddMinutes(-1);
            var user = CriarUsuarioAtivo(tentativas: 5, bloqueioAte: bloqueioExpirado);
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "joao", Password = "Senha@123" });

            result.Sucedeu.Should().BeTrue();
        }

        [Fact(DisplayName = "Login deve informar quantas tentativas restam antes do bloqueio")]
        public async Task Login_SenhaErrada_DeveInformarTentativasRestantes()
        {
            var user = CriarUsuarioAtivo(tentativas: 2); // 2 falhas, faltam 3
            _repoMock.Setup(r => r.GetByUsernameAsync("joao")).ReturnsAsync(user);

            var result = await _authService.LoginAsync(
                new LoginDto { Username = "joao", Password = "senhaErrada" });

            result.Sucedeu.Should().BeFalse();
            result.Mensagem.Should().Contain("2"); // 5 - 3 = 2 restantes
        }
    }
}
