// ERP.Tests/Infrastructure/NotaFiscalAvulsaServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using ERP.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// Item pendente desde a auditoria de 13/08 (uma das 4 lacunas críticas
/// listadas junto de OrderProcessingService/ContaReceberService/
/// NfeContingencyWorker). Escrito depois da rodada de revisão de
/// arquitetura do S27 — foca nos achados reais dessa revisão, não em
/// cobertura exaustiva de cada branch: devolução sem chave referenciada
/// (bloqueio real de SEFAZ), pagamento real vs. "90 sem pagamento",
/// transporte, o fix de fuso do S21 reaplicado aqui, e cancelamento.
///
/// Usa TestDb (InMemory) — diferente do ContaReceberServiceTests, esse
/// service não usa ExecuteSqlInterpolatedAsync em lugar nenhum, só
/// EF Core rastreado normal.
/// </summary>
public class NotaFiscalAvulsaServiceTests
{
    private static (NotaFiscalAvulsaService Service, AppDbContext Ctx, Mock<INfeEmissionService> NfeMock,
        Mock<INfeCancellationService> CancelMock, Guid ProductId) Build(
            Guid tenantId, Action<AppDbContext>? seed = null, bool comSubstituicaoTrib = false)
    {
        var productId = Guid.NewGuid();
        var ctx = TestDbSqlite.Create(tenantId, c =>
        {
            c.Products.Add(new Product
            {
                Id = productId, TenantId = tenantId, Name = "Cimento CP-II 50kg",
                SalePrice = 35m, NCM = "25232900", CSOSN = "102", SKU = "CIM-50",
                TemSubstituicaoTrib = comSubstituicaoTrib
            });
            seed?.Invoke(c);
        });

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.Products.GetByIdAsync(productId))
            .ReturnsAsync(ctx.Products.First(p => p.Id == productId));

        var configMock = new Mock<IFiscalConfigurationProvider>();
        configMock.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfeMock = new Mock<INfeEmissionService>();
        nfeMock.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "Autorizada", "https://focusnfe.com.br/danfe/teste", "https://focusnfe.com.br/xml/teste", "", ""));

        var cancelMock = new Mock<INfeCancellationService>();
        cancelMock.Setup(s => s.CancelarNotaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), "NFE"))
            .ReturnsAsync((true, "Cancelamento homologado pela SEFAZ"));

        var statusMock = new Mock<INfeStatusService>();

        var motorFiscalMock = new Mock<IMotorFiscalService>();
        var tenant = new FakeRequestTenant { TenantId = tenantId };

        var service = new NotaFiscalAvulsaService(
            ctx, uowMock.Object, motorFiscalMock.Object, configMock.Object, nfeMock.Object, cancelMock.Object, statusMock.Object, tenant);

        return (service, ctx, nfeMock, cancelMock, productId);
    }

    private static SalvarNotaFiscalAvulsaDto DtoBase(Guid productId, string finalidade = "1") => new()
    {
        NaturezaOperacao = "VENDA DE MERCADORIA",
        TipoOperacaoEntradaSaida = "S",
        Finalidade = finalidade,
        DestinatarioNome = "Cliente Teste",
        DestinatarioDocumento = "12443095916", // CPF, evita a exigência de endereço completo (só CNPJ exige)
        Itens = new List<NotaFiscalAvulsaItemDto>
        {
            new() { ProductId = productId, ProductName = "Cimento CP-II 50kg", Quantidade = 10, ValorUnitario = 35m, Cfop = "5102" }
        }
    };

    // ── SalvarRascunhoAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Salvar rascunho novo persiste os campos de transporte e informações complementares")]
    public async Task SalvarRascunho_Novo_PersisteCamposDeTransporte()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, _, productId) = Build(tenantId);

        var dto = DtoBase(productId);
        dto.InformacoesComplementares = "Depósito bancário: Banco X, Ag 0001, CC 12345-6";
        dto.ModalidadeFrete = "1";
        dto.TransportadoraNome = "Transportadora Rápida LTDA";
        dto.TransportadoraDocumento = "11222333000181";
        dto.QuantidadeVolumes = 5;
        dto.PesoBrutoKg = 250.5m;

        var id = await service.SalvarRascunhoAsync(dto);

        var nota = await ctx.NotasFiscais.Include(n => n.Itens).AsNoTracking().SingleAsync(n => n.Id == id);
        nota.InformacoesComplementares.Should().Be(dto.InformacoesComplementares);
        nota.ModalidadeFrete.Should().Be("1");
        nota.TransportadoraNome.Should().Be("Transportadora Rápida LTDA");
        nota.QuantidadeVolumes.Should().Be(5);
        nota.PesoBrutoKg.Should().Be(250.5m);
        nota.Itens.Should().ContainSingle();
    }

    [Fact(DisplayName = "Salvar rascunho existente substitui os pagamentos por completo, não acumula")]
    public async Task SalvarRascunho_Existente_SubstituiPagamentos()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, _, productId) = Build(tenantId);

        var dto = DtoBase(productId);
        dto.Pagamentos = new List<NotaFiscalAvulsaPagamentoDto> { new() { FormaPagamento = "01", Valor = 350m } };
        var id = await service.SalvarRascunhoAsync(dto);

        // Salva de novo, com pagamento diferente — o antigo não pode sobrar.
        dto.Id = id;
        dto.Pagamentos = new List<NotaFiscalAvulsaPagamentoDto> { new() { FormaPagamento = "17", Valor = 175m }, new() { FormaPagamento = "03", Valor = 175m } };
        await service.SalvarRascunhoAsync(dto);

        var nota = await ctx.NotasFiscais.Include(n => n.Pagamentos).AsNoTracking().SingleAsync(n => n.Id == id);
        nota.Pagamentos.Should().HaveCount(2);
        nota.Pagamentos.Should().NotContain(p => p.FormaPagamento == "01");
    }

    // ── CopiarComoRascunhoAsync ──────────────────────────────────────────

    [Fact(DisplayName = "Copiar rascunho NUNCA copia a chave referenciada (cada devolução referencia uma venda diferente)")]
    public async Task CopiarRascunho_NuncaCopiaChaveReferenciada()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, _, productId) = Build(tenantId);

        var dto = DtoBase(productId, finalidade: "4");
        dto.RefNfeReferenciada = "41260812820608000141550010000012341234123412";
        dto.TransportadoraNome = "Transportadora X"; // isso, sim, deve copiar
        var origemId = await service.SalvarRascunhoAsync(dto);

        var copiaId = await service.CopiarComoRascunhoAsync(origemId);

        var copia = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == copiaId);
        copia.RefNFe.Should().BeNullOrEmpty("cada devolução referencia uma venda original diferente");
        copia.TransportadoraNome.Should().Be("Transportadora X", "transporte tende a se repetir, faz sentido copiar");
        copia.Status.Should().Be("Rascunho");
    }

    // ── ExcluirRascunhoAsync ─────────────────────────────────────────────

    [Fact(DisplayName = "Excluir rascunho remove itens e pagamentos junto (sem deixar órfão)")]
    public async Task ExcluirRascunho_RemoveItensEPagamentosJunto()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, _, productId) = Build(tenantId);

        var dto = DtoBase(productId);
        dto.Pagamentos = new List<NotaFiscalAvulsaPagamentoDto> { new() { FormaPagamento = "17", Valor = 350m } };
        var id = await service.SalvarRascunhoAsync(dto);

        await service.ExcluirRascunhoAsync(id);

        (await ctx.NotasFiscais.AsNoTracking().AnyAsync(n => n.Id == id)).Should().BeFalse();
        (await ctx.NotaFiscalPagamentos.AsNoTracking().AnyAsync(p => p.NotaFiscalId == id)).Should().BeFalse();
    }

    [Fact(DisplayName = "Não permite excluir nota que já não é mais Rascunho")]
    public async Task ExcluirRascunho_NotaJaEmitida_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        await service.EmitirAsync(id);

        var act = async () => await service.ExcluirRascunhoAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── EmitirAsync — devolução sem chave referenciada ──────────────────

    [Fact(DisplayName = "CRÍTICO: devolução sem chave referenciada é bloqueada antes de chegar na Focus")]
    public async Task Emitir_DevolucaoSemChave_Bloqueada()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId, finalidade: "4"));

        var act = async () => await service.EmitirAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*chave de acesso*");
        nfeMock.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact(DisplayName = "CRÍTICO: devolução com chave de tamanho errado é bloqueada")]
    public async Task Emitir_DevolucaoComChaveTamanhoErrado_Bloqueada()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var dto = DtoBase(productId, finalidade: "4");
        dto.RefNfeReferenciada = "123456"; // bem menos que 44 dígitos
        var id = await service.SalvarRascunhoAsync(dto);

        var act = async () => await service.EmitirAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*44 dígitos*");
        nfeMock.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact(DisplayName = "Devolução com chave válida declara notas_referenciadas certinho")]
    public async Task Emitir_DevolucaoComChaveValida_DeclaraNotaReferenciada()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var dto = DtoBase(productId, finalidade: "4");
        dto.RefNfeReferenciada = "4126 0812 8206 0800 0141 5500 1000 0012 3412 3412 3412"; // 44 dígitos com espaços, como o usuário digitaria
        var id = await service.SalvarRascunhoAsync(dto);

        await service.EmitirAsync(id);

        nfeMock.Verify(s => s.EmitirNfeA4Async(
            It.IsAny<string>(),
            It.Is<FocusNfceRequest>(req =>
                req.NotasReferenciadas != null &&
                req.NotasReferenciadas.Count == 1 &&
                req.NotasReferenciadas[0].ChaveNfe == "41260812820608000141550010000012341234123412"), // só os dígitos (44)
            It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    // ── EmitirAsync — pagamento real vs. "90 sem pagamento" ─────────────

    [Fact(DisplayName = "Sem pagamento informado, cai em \"90 sem pagamento\" com o total dos itens (comportamento original preservado)")]
    public async Task Emitir_SemPagamento_CaiEm90SemPagamento()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId)); // sem Pagamentos

        await service.EmitirAsync(id);

        nfeMock.Verify(s => s.EmitirNfeA4Async(
            It.IsAny<string>(),
            It.Is<FocusNfceRequest>(req =>
                req.Pagamentos!.Count == 1 &&
                req.Pagamentos[0].FormaPagamento == "90" &&
                req.Pagamentos[0].ValorPagamento == "350.00"), // 10 x 35,00
            It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact(DisplayName = "CRÍTICO: pagamento real (venda B2B) vai pra Focus com a forma de verdade, não mais sempre 90")]
    public async Task Emitir_ComPagamentoReal_UsaFormaVerdadeira()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var dto = DtoBase(productId);
        dto.Pagamentos = new List<NotaFiscalAvulsaPagamentoDto>
        {
            new() { FormaPagamento = "17", Valor = 200m },
            new() { FormaPagamento = "03", Valor = 150m },
        };
        var id = await service.SalvarRascunhoAsync(dto);

        await service.EmitirAsync(id);

        nfeMock.Verify(s => s.EmitirNfeA4Async(
            It.IsAny<string>(),
            It.Is<FocusNfceRequest>(req =>
                req.Pagamentos!.Count == 2 &&
                req.Pagamentos.Any(p => p.FormaPagamento == "17" && p.ValorPagamento == "200.00") &&
                req.Pagamentos.Any(p => p.FormaPagamento == "03" && p.ValorPagamento == "150.00")),
            It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    // ── EmitirAsync — transporte ─────────────────────────────────────────

    [Fact(DisplayName = "Transporte com transportadora CNPJ declara cnpj_transportador, não cpf_transportador")]
    public async Task Emitir_ComTransportadoraCnpj_DeclaraCnpjTransportador()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var dto = DtoBase(productId);
        dto.ModalidadeFrete = "1";
        dto.TransportadoraNome = "Transportes Ligeiro LTDA";
        dto.TransportadoraDocumento = "11.222.333/0001-81";
        var id = await service.SalvarRascunhoAsync(dto);

        await service.EmitirAsync(id);

        nfeMock.Verify(s => s.EmitirNfeA4Async(
            It.IsAny<string>(),
            It.Is<FocusNfceRequest>(req =>
                req.CnpjTransportador == "11222333000181" &&
                req.CpfTransportador == null &&
                req.NomeTransportador == "Transportes Ligeiro LTDA" &&
                req.ModalidadeFrete == "1"),
            It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact(DisplayName = "Sem transportadora (retirada própria), modalidade fica 9 e nada de transportadora é mandado")]
    public async Task Emitir_SemTransportadora_Modalidade9() 
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId)); // ModalidadeFrete default = "9"

        await service.EmitirAsync(id);

        nfeMock.Verify(s => s.EmitirNfeA4Async(
            It.IsAny<string>(),
            It.Is<FocusNfceRequest>(req => req.ModalidadeFrete == "9" && req.NomeTransportador == null),
            It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    // ── EmitirAsync — fuso horário (S21 reaplicado aqui) ────────────────

    [Fact(DisplayName = "DataEmissao usa o fuso do Brasil, não a hora crua do servidor (mesmo fix do S21)")]
    public async Task Emitir_DataEmissao_UsaFusoDoBrasil()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, nfeMock, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        var antes = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();

        await service.EmitirAsync(id);

        var depois = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
        nfeMock.Verify(s => s.EmitirNfeA4Async(
            It.IsAny<string>(),
            It.Is<FocusNfceRequest>(req =>
                req.DataEmissao != null && req.DataEmissao.EndsWith("-03:00") &&
                DateTime.Parse(req.DataEmissao.Substring(0, 19)) >= antes.AddSeconds(-2) &&
                DateTime.Parse(req.DataEmissao.Substring(0, 19)) <= depois.AddSeconds(2)),
            It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    // ── EmitirAsync — sucesso e regras já existentes ────────────────────

    [Fact(DisplayName = "Emissão bem-sucedida marca Autorizada e salva a URL do DANFE")]
    public async Task Emitir_Sucesso_MarcaAutorizadaESalvaUrlDanfe()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));

        var resultado = await service.EmitirAsync(id);

        resultado.Sucesso.Should().BeTrue();
        var nota = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == id);
        nota.Status.Should().Be("Autorizada");
        nota.UrlDanfe.Should().Be("https://focusnfe.com.br/danfe/teste");
    }

    [Fact(DisplayName = "Não permite emitir a mesma nota duas vezes")]
    public async Task Emitir_NotaJaEmitida_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, _, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        await service.EmitirAsync(id);

        var act = async () => await service.EmitirAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Produto sem NCM válido bloqueia a emissão (NF-e valida NCM de verdade, diferente da NFC-e)")]
    public async Task Emitir_ProdutoSemNcm_Bloqueia()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var ctx = TestDbSqlite.Create(tenantId, c =>
            c.Products.Add(new Product { Id = productId, TenantId = tenantId, Name = "Produto sem NCM", SalePrice = 10m, NCM = null }));

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.Products.GetByIdAsync(productId)).ReturnsAsync(ctx.Products.First(p => p.Id == productId));
        var configMock = new Mock<IFiscalConfigurationProvider>();
        configMock.Setup(c => c.ObterConfiguracaoAsync()).ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "t", UsarAmbienteProducao = false });
        var tenant = new FakeRequestTenant { TenantId = tenantId };
        var service = new NotaFiscalAvulsaService(ctx, uowMock.Object, new Mock<IMotorFiscalService>().Object,
            configMock.Object, new Mock<INfeEmissionService>().Object, new Mock<INfeCancellationService>().Object,
            new Mock<INfeStatusService>().Object, tenant);

        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        var act = async () => await service.EmitirAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*NCM*");
    }

    // ── CancelarAsync ────────────────────────────────────────────────────

    [Fact(DisplayName = "Cancela nota Autorizada com sucesso, muda status pra Cancelada")]
    public async Task Cancelar_NotaAutorizada_Cancela()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, cancelMock, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        await service.EmitirAsync(id);

        var resultado = await service.CancelarAsync(id, "Cliente desistiu da compra, mercadoria não foi entregue.");

        resultado.Sucesso.Should().BeTrue();
        var nota = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == id);
        nota.Status.Should().Be("Cancelada");
        cancelMock.Verify(c => c.CancelarNotaAsync($"avulsa-{id}", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), "NFE"), Times.Once);
    }

    [Fact(DisplayName = "CRÍTICO: não permite cancelar nota que ainda é Rascunho")]
    public async Task Cancelar_NotaRascunho_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, _, cancelMock, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));

        var act = async () => await service.CancelarAsync(id, "Motivo qualquer com mais de 15 caracteres.");

        await act.Should().ThrowAsync<InvalidOperationException>();
        cancelMock.Verify(c => c.CancelarNotaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "Justificativa curta demais (menos de 15 caracteres) é rejeitada — exigência da SEFAZ")]
    public async Task Cancelar_JustificativaCurta_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, _, _, _, productId) = Build(tenantId);
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        await service.EmitirAsync(id);

        var act = async () => await service.CancelarAsync(id, "curto demais");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Falha no cancelamento (Focus recusa) não muda o status da nota")]
    public async Task Cancelar_FalhaNaFocus_NaoMudaStatus()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx, _, cancelMock, productId) = Build(tenantId);
        cancelMock.Setup(c => c.CancelarNotaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), "NFE"))
            .ReturnsAsync((false, "Prazo de 24h pra cancelamento expirado"));
        var id = await service.SalvarRascunhoAsync(DtoBase(productId));
        await service.EmitirAsync(id);

        var resultado = await service.CancelarAsync(id, "Tentativa de cancelamento fora do prazo.");

        resultado.Sucesso.Should().BeFalse();
        var nota = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == id);
        nota.Status.Should().Be("Autorizada", "cancelamento recusado pela SEFAZ não pode mudar o status local");
    }
}