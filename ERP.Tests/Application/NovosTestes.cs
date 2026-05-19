// ERP.Tests/Application/CustomerServiceTests.cs
// ERP.Tests/Application/PedidoCompraServiceTests.cs  
// ERP.Tests/Application/NfeContingencyServiceTests.cs
//
// Todos num único arquivo para facilitar a entrega.
// Separe em arquivos individuais se quiser.

using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ERP.Tests.Application;

// ═══════════════════════════════════════════════════════════════════════════════
//  HELPER
// ═══════════════════════════════════════════════════════════════════════════════
internal static class Db
{
    public static IServiceProvider Build(string name, Action<AppDbContext>? seed = null)
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);

        var svc = new ServiceCollection();
        svc.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(name));
        var sp = svc.BuildServiceProvider();

        if (seed != null)
        {
            using var scope = sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seed(ctx);
            ctx.SaveChanges();
        }
        return sp;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CUSTOMER SERVICE TESTS
// ═══════════════════════════════════════════════════════════════════════════════
public class CustomerServiceTests
{
    // ── helper: monta mock de IUnitOfWork com CustomerRepository in-memory ───
    private static (CustomerService svc, AppDbContext ctx, IServiceProvider sp) BuildSvc(string dbName)
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);

        var svc = new ServiceCollection();
        svc.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = svc.BuildServiceProvider();
        var ctx = sp.GetRequiredService<AppDbContext>();

        var uowMock     = new Mock<IUnitOfWork>();
        var customerRepo = new Mock<ICustomerRepository>();
        uowMock.Setup(u => u.Customers).Returns(customerRepo.Object);
        uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var mapperMock    = new Mock<AutoMapper.IMapper>();
        var validatorMock = new Mock<FluentValidation.IValidator<CreateCustomerDto>>();

        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<FluentValidation.ValidationContext<CreateCustomerDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var service = new CustomerService(uowMock.Object, mapperMock.Object, validatorMock.Object);
        return (service, ctx, sp);
    }

    [Fact]
    public async Task DeleteAsync_ClienteNaoEncontrado_LancaKeyNotFound()
    {
        var uow  = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Customer?)null);
        uow.Setup(u => u.Customers).Returns(repo.Object);

        var svc = new CustomerService(uow.Object,
            new Mock<AutoMapper.IMapper>().Object,
            new Mock<FluentValidation.IValidator<CreateCustomerDto>>().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_ClienteExistente_MarcaIsDeleted()
    {
        var cliente = new Customer { Id = Guid.NewGuid(), Name = "Teste", IsDeleted = false };

        var uow  = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerRepository>();
        repo.Setup(r => r.GetByIdAsync(cliente.Id)).ReturnsAsync(cliente);
        uow.Setup(u => u.Customers).Returns(repo.Object);
        uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var svc = new CustomerService(uow.Object,
            new Mock<AutoMapper.IMapper>().Object,
            new Mock<FluentValidation.IValidator<CreateCustomerDto>>().Object);

        await svc.DeleteAsync(cliente.Id);

        cliente.IsDeleted.Should().BeTrue();
        repo.Verify(r => r.Update(It.Is<Customer>(c => c.IsDeleted)), Times.Once);
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ClienteComDocumentoDuplicado_ReativaExistente()
    {
        var clienteExistente = new Customer
        {
            Id        = Guid.NewGuid(),
            Name      = "Nome Antigo",
            Document  = "12345678000199",
            IsDeleted = true,
        };

        var uow  = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerRepository>();
        repo.Setup(r => r.GetByDocumentAsync("12345678000199")).ReturnsAsync(clienteExistente);
        uow.Setup(u => u.Customers).Returns(repo.Object);
        uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var mapperMock = new Mock<AutoMapper.IMapper>();
        mapperMock.Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>()))
            .Returns(new CustomerDto(Guid.NewGuid(), "Nome Novo", "12345678000199", null, null, 0, null, null, null));

        var validatorMock = new Mock<FluentValidation.IValidator<CreateCustomerDto>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<FluentValidation.ValidationContext<CreateCustomerDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var svc = new CustomerService(uow.Object, mapperMock.Object, validatorMock.Object);

        var dto = new CreateCustomerDto { Name = "Nome Novo", Document = "12345678000199" };
        await svc.CreateAsync(dto);

        // Deve ter reativado o cliente existente
        clienteExistente.IsDeleted.Should().BeFalse();
        clienteExistente.Name.Should().Be("Nome Novo");
        repo.Verify(r => r.Update(It.IsAny<Customer>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DocumentoVazio_CriaNovoCadastro()
    {
        var uow  = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerRepository>();
        // Documento vazio não chama GetByDocumentAsync
        uow.Setup(u => u.Customers).Returns(repo.Object);
        uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var mapperMock = new Mock<AutoMapper.IMapper>();
        mapperMock.Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>()))
            .Returns(new CustomerDto(Guid.NewGuid(), "Cliente Sem Doc", null, null, null, 0, null, null, null));

        var validatorMock = new Mock<FluentValidation.IValidator<CreateCustomerDto>>();
        validatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<FluentValidation.ValidationContext<CreateCustomerDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var svc = new CustomerService(uow.Object, mapperMock.Object, validatorMock.Object);

        var dto = new CreateCustomerDto { Name = "Cliente Sem Doc", Document = "" };
        await svc.CreateAsync(dto);

        repo.Verify(r => r.GetByDocumentAsync(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ClienteNaoEncontrado_LancaKeyNotFound()
    {
        var uow  = new Mock<IUnitOfWork>();
        var repo = new Mock<ICustomerRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Customer?)null);
        uow.Setup(u => u.Customers).Returns(repo.Object);

        var svc = new CustomerService(uow.Object,
            new Mock<AutoMapper.IMapper>().Object,
            new Mock<FluentValidation.IValidator<CreateCustomerDto>>().Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.UpdateAsync(Guid.NewGuid(), new CreateCustomerDto { Name = "X" }));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  PEDIDO COMPRA SERVICE TESTS
// ═══════════════════════════════════════════════════════════════════════════════
public class PedidoCompraServiceTests
{
    private static (ERP.Application.Services.PedidoCompraService svc,
                    Mock<IUnitOfWork> uow,
                    Mock<IPedidoCompraRepository> repo,
                    Mock<IProductRepository> prodRepo) Build()
    {
        var uow      = new Mock<IUnitOfWork>();
        var repo     = new Mock<IPedidoCompraRepository>();
        var prodRepo = new Mock<IProductRepository>();

        repo.Setup(r => r.GerarProximoNumeroAsync()).ReturnsAsync("PC-2026-001");
        repo.Setup(r => r.AddAsync(It.IsAny<PedidoCompra>())).Returns(Task.CompletedTask);
        uow.Setup(u => u.PedidosCompra).Returns(repo.Object);
        uow.Setup(u => u.Products).Returns(prodRepo.Object);
        uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        return (new ERP.Application.Services.PedidoCompraService(uow.Object), uow, repo, prodRepo);
    }

    [Fact]
    public async Task CriarAsync_PedidoComItens_PersisteCom1Commit()
    {
        var (svc, uow, repo, _) = Build();

        var dto = new CreatePedidoCompraDto
        {
            FornecedorNome = "Fornecedor A",
            CriadoPor      = "Tester",
            Itens = new List<CreatePedidoCompraItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Produto X", Quantidade = 10, PrecoUnitario = 5 }
            }
        };

        var result = await svc.CriarAsync(dto);

        result.Numero.Should().Be("PC-2026-001");
        result.Itens.Should().HaveCount(1);
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_StatusRascunho_MudaParaEnviado()
    {
        var (svc, uow, repo, _) = Build();
        var pedido = new PedidoCompra { Status = StatusPedidoCompra.Rascunho };
        pedido.Itens.Add(new PedidoCompraItem { ProductId = Guid.NewGuid(), Quantidade = 1 });

        repo.Setup(r => r.GetByIdAsync(pedido.Id)).ReturnsAsync(pedido);

        await svc.EnviarAsync(pedido.Id);

        pedido.Status.Should().Be(StatusPedidoCompra.Enviado);
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_PedidoSemItens_LancaInvalidOperation()
    {
        var (svc, _, repo, _) = Build();
        var pedido = new PedidoCompra { Status = StatusPedidoCompra.Rascunho };
        // sem itens
        repo.Setup(r => r.GetByIdAsync(pedido.Id)).ReturnsAsync(pedido);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EnviarAsync(pedido.Id));
    }

    [Fact]
    public async Task ReceberAsync_PedidoEnviado_AtualizaEstoqueDoProduto()
    {
        var (svc, uow, repo, prodRepo) = Build();
        var prodId = Guid.NewGuid();
        var produto = new Product { Id = prodId, Name = "P1", Stock = 5, SalePrice = 10 };

        var pedido = new PedidoCompra { Status = StatusPedidoCompra.Enviado };
        pedido.Itens.Add(new PedidoCompraItem { ProductId = prodId, Quantidade = 10, PrecoUnitario = 8 });

        repo.Setup(r => r.GetWithItensAsync(pedido.Id)).ReturnsAsync(pedido);
        prodRepo.Setup(r => r.GetByIdAsync(prodId)).ReturnsAsync(produto);

        await svc.ReceberAsync(pedido.Id);

        pedido.Status.Should().Be(StatusPedidoCompra.Recebido);
        produto.Stock.Should().Be(15);       // 5 + 10
        produto.CostPrice.Should().Be(8);    // atualizado com PrecoUnitario
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelarAsync_PedidoJaRecebido_LancaInvalidOperation()
    {
        var (svc, _, repo, _) = Build();
        var pedido = new PedidoCompra { Status = StatusPedidoCompra.Recebido };
        repo.Setup(r => r.GetByIdAsync(pedido.Id)).ReturnsAsync(pedido);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelarAsync(pedido.Id));
    }

    [Fact]
    public async Task DeletarAsync_PedidoNaoEncontrado_LancaKeyNotFound()
    {
        var (svc, _, repo, _) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((PedidoCompra?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.DeletarAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CicloCompleto_RascunhoEnviadoRecebido_EstoqueCorreto()
    {
        // Testa o ciclo completo em sequência
        var (svc, uow, repo, prodRepo) = Build();
        var prodId  = Guid.NewGuid();
        var produto = new Product { Id = prodId, Name = "Tijolo", Stock = 100, SalePrice = 1 };

        var pedido = new PedidoCompra { Status = StatusPedidoCompra.Rascunho };
        pedido.Itens.Add(new PedidoCompraItem { ProductId = prodId, Quantidade = 50, PrecoUnitario = 0.80m });

        repo.Setup(r => r.GetByIdAsync(pedido.Id)).ReturnsAsync(pedido);
        repo.Setup(r => r.GetWithItensAsync(pedido.Id)).ReturnsAsync(pedido);
        prodRepo.Setup(r => r.GetByIdAsync(prodId)).ReturnsAsync(produto);

        await svc.EnviarAsync(pedido.Id);
        pedido.Status.Should().Be(StatusPedidoCompra.Enviado);

        await svc.ReceberAsync(pedido.Id);
        pedido.Status.Should().Be(StatusPedidoCompra.Recebido);
        produto.Stock.Should().Be(150);
        produto.CostPrice.Should().Be(0.80m);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  NFE CONTINGENCY SERVICE TESTS
// ═══════════════════════════════════════════════════════════════════════════════
public class NfeContingencyServiceTests
{
    private static (NfeContingencyService svc,
                    Mock<IUnitOfWork> uow,
                    Mock<INfePendenteRepository> repo) Build()
    {
        var uow  = new Mock<IUnitOfWork>();
        var repo = new Mock<INfePendenteRepository>();

        uow.Setup(u => u.NfePendentes).Returns(repo.Object);
        uow.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        var http = new Mock<IFocusNfeHttpClient>();
        return (new NfeContingencyService(http.Object, uow.Object), uow, repo);
    }

    [Fact]
    public async Task RegistrarNotaPendenteAsync_PersisteCom1Commit()
    {
        var (svc, uow, repo) = Build();
        var vendaId = Guid.NewGuid();

        await svc.RegistrarNotaPendenteAsync(vendaId, "NFCE", "{\"dados\":\"teste\"}");

        repo.Verify(r => r.AddAsync(It.Is<NfePendente>(n =>
            n.VendaId   == vendaId &&
            n.TipoNota  == "NFCE" &&
            n.Tentativas == 0)),
            Times.Once);
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task ObterNotasPendentesAsync_RetornaOrdenadoPorDataFalha()
    {
        var (svc, _, repo) = Build();
        var notas = new List<NfePendente>
        {
            new() { Id = Guid.NewGuid(), DataFalha = DateTime.Now.AddMinutes(-5) },
            new() { Id = Guid.NewGuid(), DataFalha = DateTime.Now.AddMinutes(-1) },
            new() { Id = Guid.NewGuid(), DataFalha = DateTime.Now.AddMinutes(-10) },
        };
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(notas);

        var result = (await svc.ObterNotasPendentesAsync()).ToList();

        result.Should().BeInAscendingOrder(n => n.DataFalha);
    }

    [Fact]
    public async Task RemoverNotaPendenteAsync_NotaExistente_RemoveECommita()
    {
        var (svc, uow, repo) = Build();
        var nota = new NfePendente { Id = Guid.NewGuid() };
        repo.Setup(r => r.GetByIdAsync(nota.Id)).ReturnsAsync(nota);

        await svc.RemoverNotaPendenteAsync(nota.Id);

        repo.Verify(r => r.Remove(nota), Times.Once);
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoverNotaPendenteAsync_NotaNaoEncontrada_NaoFazNada()
    {
        var (svc, uow, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((NfePendente?)null);

        // Não deve lançar exceção
        await svc.RemoverNotaPendenteAsync(Guid.NewGuid());

        repo.Verify(r => r.Remove(It.IsAny<NfePendente>()), Times.Never);
        uow.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task RegistrarFalhaTentativaAsync_IncrementaTentativasESalvaErro()
    {
        var (svc, uow, repo) = Build();
        var nota = new NfePendente { Id = Guid.NewGuid(), Tentativas = 2 };
        repo.Setup(r => r.GetByIdAsync(nota.Id)).ReturnsAsync(nota);

        await svc.RegistrarFalhaTentativaAsync(nota.Id, "Timeout SEFAZ");

        nota.Tentativas.Should().Be(3);
        nota.UltimaMensagemErro.Should().Be("Timeout SEFAZ");
        repo.Verify(r => r.Update(nota), Times.Once);
        uow.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task RegistrarFalhaTentativaAsync_NotaNaoEncontrada_NaoLancaExcecao()
    {
        var (svc, uow, repo) = Build();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((NfePendente?)null);

        await svc.RegistrarFalhaTentativaAsync(Guid.NewGuid(), "erro");

        uow.Verify(u => u.CommitAsync(), Times.Never);
    }
}