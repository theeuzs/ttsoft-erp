using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application
{
    [Trait("Categoria", "Produtos")]
    public class ProductServiceTests
    {
        private readonly Mock<IUnitOfWork>              _uowMock;
        private readonly Mock<IMapper>                  _mapperMock;
        private readonly Mock<IValidator<CreateProductDto>> _validatorMock;
        private readonly ProductService                 _service;

        public ProductServiceTests()
        {
            _uowMock       = new Mock<IUnitOfWork>();
            _mapperMock    = new Mock<IMapper>();
            _validatorMock = new Mock<IValidator<CreateProductDto>>();

            _validatorMock
                .Setup(v => v.ValidateAsync(
                    It.IsAny<ValidationContext<CreateProductDto>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            _service = new ProductService(
                _uowMock.Object,
                _mapperMock.Object,
                _validatorMock.Object);
        }

        // ── Criação ────────────────────────────────────────────────────────────

        [Fact(DisplayName = "Criar produto deve chamar AddAsync e CommitAsync")]
        public async Task CriarProduto_DadosValidos_DevePersistir()
        {
            var dto = new CreateProductDto { Name = "Cimento CP-II 50kg", SalePrice = 38.90m };
            var entity = new Product { Id = Guid.NewGuid(), Name = dto.Name };

            _mapperMock.Setup(m => m.Map<Product>(dto)).Returns(entity);
            _uowMock.Setup(u => u.Products.AddAsync(entity)).Returns(Task.CompletedTask);

            await _service.CreateAsync(dto);

            _uowMock.Verify(u => u.Products.AddAsync(entity), Times.Once);
            _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact(DisplayName = "Criar produto com validação inválida deve lançar exceção")]
        public async Task CriarProduto_ValidacaoInvalida_DeveLancarExcecao()
        {
            var dto = new CreateProductDto { Name = "" }; // Nome vazio
            var failures = new[] { new ValidationFailure("Name", "Nome é obrigatório") };

            _validatorMock
                .Setup(v => v.ValidateAsync(
                    It.IsAny<ValidationContext<CreateProductDto>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult(failures));

            await _service.Invoking(s => s.CreateAsync(dto))
                .Should().ThrowAsync<ValidationException>();
        }

        // ── Atualização e rastreamento de preço ──────────────────────────────

        [Fact(DisplayName = "Atualizar preço deve registrar SalePriceChangedAt e By")]
        public async Task AtualizarProduto_AlterandoPreco_DeveRegistrarRastreamento()
        {
            var produtoId   = Guid.NewGuid();
            var usuarioId   = Guid.NewGuid();
            var usuarioNome = "Gerente Silva";

            var produtoExistente = new Product
            {
                Id        = produtoId,
                Name      = "Areia Média",
                SalePrice = 80.00m // Preço antigo
            };

            var dto = new UpdateProductDto
            {
                Id        = produtoId,
                Name      = "Areia Média",
                SalePrice = 95.00m // Preço novo
            };

            _uowMock
                .Setup(u => u.Products.GetByIdTrackedAsync(produtoId))
                .ReturnsAsync(produtoExistente);

            // Act
            // Simula usuário logado (ProductService usa ERP.Domain.CurrentUser.Name internamente)
            ERP.Domain.CurrentUser.Id   = usuarioId;
            ERP.Domain.CurrentUser.Name = usuarioNome;
            await _service.UpdateAsync(dto);

            // Assert — rastreamento de preço deve ter sido registrado
            produtoExistente.SalePriceChangedAt.Should().NotBeNull()
                .And.Subject.As<DateTime?>()!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            produtoExistente.SalePriceChangedBy.Should().Be(usuarioNome);
            _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact(DisplayName = "Atualizar produto sem alterar preço não deve tocar SalePriceChangedAt")]
        public async Task AtualizarProduto_SemAlterarPreco_NaoDeveRegistrarRastreamento()
        {
            var produtoId = Guid.NewGuid();
            var precoOriginal = 50.00m;

            var produtoExistente = new Product
            {
                Id        = produtoId,
                Name      = "Brita 1",
                SalePrice = precoOriginal,
                SalePriceChangedAt = null
            };

            var dto = new UpdateProductDto
            {
                Id        = produtoId,
                Name      = "Brita 1 Lavada", // só mudou o nome
                SalePrice = precoOriginal
            };

            _uowMock
                .Setup(u => u.Products.GetByIdTrackedAsync(produtoId))
                .ReturnsAsync(produtoExistente);

            ERP.Domain.CurrentUser.Name = "admin";
            await _service.UpdateAsync(dto);

            // SalePriceChangedAt não deve ter sido alterado
            produtoExistente.SalePriceChangedAt.Should().BeNull();
        }

        // ── Estoque ───────────────────────────────────────────────────────────

        [Fact(DisplayName = "Produto composto: venda deve baixar estoque do produto pai")]
        public Task BaixarEstoque_ProdutoComposto_DeveBaixarDoPai()
        {
            var paiId  = Guid.NewGuid();
            var filhoId = Guid.NewGuid();

            var pai = new Product
            {
                Id    = paiId,
                Name  = "Tubo PVC 6m",
                Stock = 100m
            };
            var filho = new Product
            {
                Id               = filhoId,
                Name             = "Tubo PVC por metro",
                ParentProductId  = paiId,
                ConversionFactor = 6m, // 1 unidade do filho = 6m do pai
                Stock            = 0m
            };

            // Vendendo 2 metros (filho) — valida a fórmula de conversão
            var qtdVendida = 2m;
            var baixaEsperadaNoPai = qtdVendida * filho.ConversionFactor; // 2 * 6 = 12m

            pai.Stock -= baixaEsperadaNoPai;

            pai.Stock.Should().Be(88m); // 100 - 12 = 88
            return Task.CompletedTask;
        }
    }
}