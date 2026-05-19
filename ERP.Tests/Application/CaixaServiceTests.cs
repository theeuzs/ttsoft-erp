using ERP.Application.DTOs;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

public class CaixaServiceTests
{
    private readonly Mock<ICaixaRepository> _caixaRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CaixaService _caixaService;

    public CaixaServiceTests()
    {
        // 1. Criamos os dublês
        _caixaRepoMock = new Mock<ICaixaRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        // 👇 A MÁGICA ARQUITETURAL DO MOQ: 
        // Ensinamos o dublê do UnitOfWork a devolver o nosso dublê do Repositório!
        _unitOfWorkMock.Setup(u => u.Caixas).Returns(_caixaRepoMock.Object);

        // 2. Entregamos SÓ o UnitOfWork para o Service (exatamente como o seu código exige)
        _caixaService = new CaixaService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task AbrirCaixaAsync_DeveLancarExcecao_QuandoUsuarioJaTemCaixaAberto()
    {
        // Arrange (Preparação com o seu DTO real)
        var usuarioId = Guid.NewGuid();
        var dto = new AbrirCaixaDto
        {
            UsuarioId = usuarioId,
            OperadorNome = "Matheus Silva",
            ValorAbertura = 100m
        };

        // Ensinamos o dublê a MENTIR dizendo que o caixa já está aberto
        var caixaJaExistente = new Caixa { Id = Guid.NewGuid(), UsuarioId = usuarioId, Status = StatusCaixa.Aberto };
        
        // Usando o nome exato do seu método no Repositório
        _caixaRepoMock.Setup(repo => repo.GetCaixaAbertoByUsuarioAsync(usuarioId))
                      .ReturnsAsync(caixaJaExistente);

        // Act (Ação: Tentamos abrir um caixa enviando o DTO)
        Func<Task> acao = async () => await _caixaService.AbrirCaixaAsync(dto);

        // Assert (Verificação final)
        // Usamos o InvalidOperationException e a mensagem EXATA que você escreveu no seu Service
        await acao.Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("Você já tem um caixa aberto!"); 
        
        // Garante que o método que salva no banco (Commit) NUNCA foi chamado
        _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
    }
}