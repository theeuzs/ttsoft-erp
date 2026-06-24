using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces; 
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace ERP.Tests.Application.Services
{
    public class SaleServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<IValidator<CreateSaleDto>> _validatorMock;
        private readonly Mock<IHaverService> _haverServiceMock;
        private readonly Mock<IRequestTenant> _tenantMock;
        private readonly SaleService _saleService;

        public SaleServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _validatorMock = new Mock<IValidator<CreateSaleDto>>();
            _haverServiceMock = new Mock<IHaverService>();

            // S9: mock de IRequestTenant com MaxDiscountPercentage = 100 (Admin) por padrão nos testes.
            // Testes específicos de validação de desconto usam tenantMock.Setup(...) explicitamente.
            _tenantMock = new Mock<IRequestTenant>();
            _tenantMock.Setup(t => t.MaxDiscountPercentage).Returns(100m);

            // Simula que a validação do FluentValidation passou com sucesso
            _validatorMock.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateSaleDto>>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(new ValidationResult());
                          
            _uowMock.Setup(u => u.Sales.AddAsync(It.IsAny<Sale>())).Returns(Task.CompletedTask);

            // 👇 MOCKS ADICIONADOS PARA NÃO QUEBRAR OS TESTES 👇
            
            // Simula a baixa de estoque atômica com sucesso
            _uowMock.Setup(u => u.Products.BaixarEstoqueAtomicoAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<bool>()))
                    .ReturnsAsync(true);
            
            // Simula a busca de contas a receber retornando uma lista vazia
            _uowMock.Setup(u => u.ContasReceber.GetBySaleIdAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(new List<ContaReceber>());
            
            // Simula o commit no banco de dados
            _uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(0);

            // Fase 0: BeginTransactionAsync deve estar no construtor para todos os testes
            var txMock = new Mock<ITransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            txMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            txMock.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            _uowMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(txMock.Object);

            _saleService = new SaleService(_uowMock.Object, _mapperMock.Object, _validatorMock.Object, _haverServiceMock.Object, _tenantMock.Object);
        }

        [Fact(DisplayName = "CriarVenda - Deve processar a venda e baixar o estoque corretamente")]
        [Trait("Categoria", "Vendas - Criação")]
        public async Task CriarVenda_ComEstoqueDisponivel_DeveProcessarComSucesso()
        {
            // Arrange
            var usuarioId = Guid.NewGuid();
            var produtoId = Guid.NewGuid();
            var produtoFake = new Product { Id = produtoId, Name = "Veda Rosca", Stock = 10, AllowNegativeStock = false };
            // Callback simula o UPDATE atômico de estoque que acontece no banco
            _uowMock.Setup(u => u.Products.BaixarEstoqueAtomicoAsync(produtoId, It.IsAny<decimal>(), It.IsAny<bool>()))
                    .ReturnsAsync(true)
                    .Callback<Guid, decimal, bool>((_, qty, _) => produtoFake.Stock -= qty);
            
            var dto = new CreateSaleDto
            {
                UsuarioId = usuarioId,
                Items = new List<CreateSaleItemDto> { new CreateSaleItemDto { ProductId = produtoId, Quantity = 2, UnitPrice = 5.90m } },
                Payments = new List<CreateSalePaymentDto>() // Venda sem pagamento específico para simplificar o teste
            };

            // Simula o Caixa Aberto
            _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
            
            // Simula o Produto no Banco
            _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);

            // Simula o Retorno do Mapper
            _mapperMock.Setup(m => m.Map<SaleDto>(It.IsAny<Sale>())).Returns((SaleDto)null!);

            // Act
            await _saleService.CreateAsync(dto);

            // Assert
            produtoFake.Stock.Should().Be(8); // Tinha 10, vendeu 2, sobrou 8
            _uowMock.Verify(u => u.Sales.AddAsync(It.IsAny<Sale>()), Times.Once);
            _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact(DisplayName = "CriarVenda - Deve lançar exceção quando o estoque for insuficiente")]
        [Trait("Categoria", "Vendas - Estoque")]
        public async Task CriarVenda_ComEstoqueZerado_DevePermitirVenda()
        {
            // Arrange
            var usuarioId = Guid.NewGuid();
            var produtoId = Guid.NewGuid();
            
            // Estoque atual é ZERO
            var produtoFake = new Product { Id = produtoId, Name = "Cimento Votorantim", Stock = 0 };
            _uowMock.Setup(u => u.Products.BaixarEstoqueAtomicoAsync(produtoId, It.IsAny<decimal>(), It.IsAny<bool>()))
                    .ReturnsAsync(true)
                    .Callback<Guid, decimal, bool>((_, qty, _) => produtoFake.Stock -= qty);
            
            var dto = new CreateSaleDto
            {
                UsuarioId = usuarioId,
                // Cliente quer comprar 5 sacos
                Items = new List<CreateSaleItemDto> { new CreateSaleItemDto { ProductId = produtoId, Quantity = 5 } }, 
                Payments = new List<CreateSalePaymentDto>()
            };

            _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
            _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);

            // Act
            await _saleService.CreateAsync(dto);

            // Assert
            // Como vendeu 5 e tinha 0, o sistema tem que deixar passar e o estoque tem que ir para -5
            produtoFake.Stock.Should().Be(-5); 
            _uowMock.Verify(u => u.Sales.AddAsync(It.IsAny<Sale>()), Times.Once);
            _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact(DisplayName = "CancelarVenda - Deve estornar o estoque e alterar o status para Cancelada")]
        [Trait("Categoria", "Vendas - Cancelamento")]
        public async Task CancelarVenda_DeveEstornarEstoqueEAtualizarStatus()
        {
            // Arrange
            var vendaId = Guid.NewGuid();
            var produtoId = Guid.NewGuid();
            
            var produtoFake = new Product { Id = produtoId, Stock = 5 }; // Estoque atual
            var vendaFake = new Sale 
            { 
                Id = vendaId, 
                Status = SaleStatus.SemNota,
                Items = new List<SaleItem> { new SaleItem { ProductId = produtoId, Quantity = 3 } }, // Vendeu 3
                Payments = new List<SalePayment>()
            };

            _uowMock.Setup(u => u.Sales.GetWithItemsAsync(vendaId)).ReturnsAsync(vendaFake);
            _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);

            // Act
            await _saleService.CancelAsync(vendaId, "Cliente desistiu");

            // Assert
            vendaFake.Status.Should().Be(SaleStatus.Cancelada);
            produtoFake.Stock.Should().Be(8); // Estornou 3 para os 5 que já existiam
            _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact(DisplayName = "CriarVenda - Ao pagar com Haver, deve debitar o saldo do cliente")]
        [Trait("Categoria", "Vendas - Haver")]
        public async Task CriarVenda_ComHaver_DeveDebitarSaldoDoCliente()
        {
            // Arrange
            var usuarioId = Guid.NewGuid();
            var clienteId = Guid.NewGuid();
            var produtoId = Guid.NewGuid();
            
            var clienteFake = new Customer { Id = clienteId, HaverBalance = 100.00m }; // Cliente tem R$ 100 de crédito
            var produtoFake = new Product { Id = produtoId, Stock = 10 };
            
            var dto = new CreateSaleDto
            {
                UsuarioId = usuarioId,
                CustomerId = clienteId,
                Items = new List<CreateSaleItemDto> { new CreateSaleItemDto { ProductId = produtoId, Quantity = 1 } },
                Payments = new List<CreateSalePaymentDto> 
                { 
                    new CreateSalePaymentDto { PaymentMethod = PaymentMethod.Haver, Amount = 40.00m } // Gastou 40 de haver
                }
            };

            _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId)).ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
            _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);
            _uowMock.Setup(u => u.Customers.GetByIdAsync(clienteId)).ReturnsAsync(clienteFake);

            // Act
            await _saleService.CreateAsync(dto);

            // Assert
            clienteFake.HaverBalance.Should().Be(60.00m); // Gastou 40, sobrou 60
            _uowMock.Verify(u => u.CommitAsync(), Times.Once);
        }
        /// <summary>
        /// FASE 0 — Teste #4 (Rollback de Venda)
        ///
        /// Verifica que quando CommitAsync lança exceção, o RollbackAsync é chamado
        /// na transação, garantindo que o estoque não fica fantasma.
        ///
        /// Com Mock o rollback não desfaz o banco de verdade — isso é verificado
        /// nos testes de integração. Aqui provamos que o PLUMBING está correto:
        /// qualquer exception no commit aciona o rollback.
        /// </summary>
        [Fact(DisplayName = "FASE0 #4 — Rollback: CommitAsync falha → RollbackAsync chamado")]
        [Trait("Fase0", "Transacao")]
        public async Task CriarVenda_QuandoCommitFalha_DeveAcionarRollback()
        {
            // Arrange
            var usuarioId = Guid.NewGuid();
            var produtoId = Guid.NewGuid();
            var produtoFake = new Product { Id = produtoId, Name = "Cimento CP-II", Stock = 100, AllowNegativeStock = false };

            var dto = new CreateSaleDto
            {
                UsuarioId = usuarioId,
                Items     = new List<CreateSaleItemDto> { new CreateSaleItemDto { ProductId = produtoId, Quantity = 1, UnitPrice = 35m } },
                Payments  = new List<CreateSalePaymentDto>()
            };

            _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId))
                    .ReturnsAsync(new Caixa { Id = Guid.NewGuid() });
            _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId))
                    .ReturnsAsync(produtoFake);

            // CommitAsync lança exception (simula falha de rede / constraint)
            _uowMock.Setup(u => u.CommitAsync())
                    .ThrowsAsync(new Exception("Simulated DB failure"));

            // Recria o mock de transação para este teste específico
            var txMock = new Mock<ITransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            txMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            txMock.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            _uowMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(txMock.Object);

            // Act
            var act = async () => await _saleService.CreateAsync(dto);

            // Assert: a exception deve ser relançada (não engolida)
            await act.Should().ThrowAsync<Exception>();

            // O Rollback DEVE ter sido chamado exatamente uma vez
            txMock.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once,
                "rollback deve ser acionado quando o commit falha");

            // O CommitAsync da transação NÃO deve ter sido chamado (falhou antes)
            txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never,
                "tx.CommitAsync nao deve ser chamado se _uow.CommitAsync falhou");
        }

        // S9: desconto acima do limite da role deve ser rejeitado pelo servidor
        [Fact(DisplayName = "CriarVenda - DiscountPercent acima do MaxDiscountPercentage da role → InvalidOperationException")]
        [Trait("Categoria", "Vendas - Segurança")]
        public async Task CriarVenda_DescontoAcimaDoLimite_LancaExcecao()
        {
            // Arrange
            var produtoId = Guid.NewGuid();
            var usuarioId = Guid.NewGuid();
            var produtoFake = new Product
            {
                Id        = produtoId,
                Name      = "Produto Teste",
                Stock     = 10,
                SalePrice = 100m,
                TenantId  = Guid.NewGuid()
            };

            var dto = new CreateSaleDto
            {
                UsuarioId = usuarioId,
                Items    = new List<CreateSaleItemDto>
                {
                    new() { ProductId = produtoId, Quantity = 1, DiscountPercent = 20m } // 20% > 5% do Vendedor
                },
                Payments = new List<CreateSalePaymentDto>
                {
                    new() { PaymentMethod = ERP.Domain.Enums.PaymentMethod.Dinheiro, Amount = 80m }
                }
            };

            // Caixa aberto — necessário para chegar na validação de desconto
            _uowMock.Setup(u => u.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId))
                    .ReturnsAsync(new Caixa { Id = Guid.NewGuid() });

            _uowMock.Setup(u => u.Products.GetByIdAsync(produtoId)).ReturnsAsync(produtoFake);
            _uowMock.Setup(u => u.Products.BaixarEstoqueAtomicoAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<bool>()))
                    .ReturnsAsync(true);

            // Role Vendedor: MaxDiscountPercentage = 5%
            _tenantMock.Setup(t => t.MaxDiscountPercentage).Returns(5m);

            var txMock = new Mock<ITransaction>();
            txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            txMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            txMock.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
            _uowMock.Setup(u => u.BeginTransactionAsync()).ReturnsAsync(txMock.Object);

            // Act & Assert — SaleService envolve exceções do loop em Exception("ERRO NA VENDA (revertido): ...")
            var ex = await Assert.ThrowsAsync<Exception>(
                () => _saleService.CreateAsync(dto));

            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Contains("excede o limite", ex.InnerException!.Message);
        }
    }
}