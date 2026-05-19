using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using ERP.WPF.State; // Mantenha o namespace correto

namespace ERP.Tests.Application.Services.Security // Ou o namespace que você estava usando
{
    public class PermissionCheckerTests : IDisposable
    {
        public PermissionCheckerTests()
        {
            // Arrange Global: Antes de cada teste, garantimos que não tem ninguém logado
            AppSession.Logout();
        }

        public void Dispose()
        {
            // Clean up: Depois de cada teste, deslogamos para não sujar o próximo teste
            AppSession.Logout();
        }

        [Fact(DisplayName = "Has - Permissão existente na sessão deve retornar True")]
        [Trait("Categoria", "Segurança - Permissões")]
        public void Has_PermissaoExistente_DeveRetornarTrue()
        {
            // Arrange: Simulamos um login real passando a permissão que queremos testar!
            var permissoes = new List<string> { PermissionChecker.SaleCancel, PermissionChecker.AuditView };
            AppSession.Login(Guid.NewGuid(), "Usuário Teste", "Operador", permissoes, 0m);

            // Act
            var resultado = PermissionChecker.Has(PermissionChecker.SaleCancel);

            // Assert
            resultado.Should().BeTrue();
        }

        [Fact(DisplayName = "Has - Permissão inexistente na sessão deve retornar False")]
        [Trait("Categoria", "Segurança - Permissões")]
        public void Has_PermissaoInexistente_DeveRetornarFalse()
        {
            // Arrange: Logamos o usuário APENAS com a permissão de desconto
            var permissoes = new List<string> { PermissionChecker.SaleDiscount };
            AppSession.Login(Guid.NewGuid(), "Usuário Teste", "Operador", permissoes, 0m);

            // Act: Ele tenta cancelar uma venda
            var resultado = PermissionChecker.Has(PermissionChecker.SaleCancel);

            // Assert
            resultado.Should().BeFalse();
        }

        [Theory(DisplayName = "Has - Parâmetro nulo ou vazio deve retornar False por segurança")]
        [Trait("Categoria", "Segurança - Permissões")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Has_PermissaoNulaOuVazia_DeveRetornarFalse(string? permissaoInvalida) // O "?" resolve o aviso amarelo!
        {
            // Arrange
            var permissoes = new List<string> { PermissionChecker.SaleCancel };
            AppSession.Login(Guid.NewGuid(), "Usuário Teste", "Operador", permissoes, 0m);

            // Act
            var resultado = PermissionChecker.Has(permissaoInvalida);

            // Assert
            resultado.Should().BeFalse();
        }

        [Fact(DisplayName = "Has - Deve ignorar maiúsculas/minúsculas (Case Insensitive)")]
        [Trait("Categoria", "Segurança - Permissões")]
        public void Has_CaseInsensitive_DeveRetornarTrue()
        {
            // Arrange: No banco de dados veio tudo maiúsculo
            var permissoes = new List<string> { "SALE.CANCEL" };
            AppSession.Login(Guid.NewGuid(), "Usuário Teste", "Operador", permissoes, 0m);

            // Act: O sistema pergunta em minúsculo
            var resultado = PermissionChecker.Has("sale.cancel");

            // Assert
            resultado.Should().BeTrue(); 
        }

        [Fact(DisplayName = "GetMaxDiscountPercentage - Deve retornar o valor exato salvo na sessão")]
        [Trait("Categoria", "Segurança - Permissões")]
        public void GetMaxDiscountPercentage_DeveRetornarValorDaSessao()
        {
            // Arrange: Simulamos um login de gerente com 15.5% de desconto permitido
            AppSession.Login(Guid.NewGuid(), "Gerente Teste", "Gerente", new List<string>(), 15.5m);

            // Act
            var resultado = PermissionChecker.GetMaxDiscountPercentage();

            // Assert
            resultado.Should().Be(15.5m);
        }
    }
}