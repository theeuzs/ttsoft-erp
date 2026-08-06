// ── ERP.Infrastructure/Services/NotaFiscalAvulsaService.cs ─────────────────
using ERP.Application.DTOs;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ERP.Infrastructure.Services;

public class NotaFiscalAvulsaService : INotaFiscalAvulsaService
{
    private readonly Persistence.Context.AppDbContext _ctx;
    private readonly IUnitOfWork _uow;
    private readonly IMotorFiscalService _motorFiscal;
    private readonly IFiscalConfigurationProvider _configProvider;
    private readonly INfeEmissionService _nfeService;
    private readonly IRequestTenant _tenant;

    public NotaFiscalAvulsaService(
        Persistence.Context.AppDbContext ctx, IUnitOfWork uow, IMotorFiscalService motorFiscal,
        IFiscalConfigurationProvider configProvider, INfeEmissionService nfeService, IRequestTenant tenant)
    {
        _ctx            = ctx;
        _uow            = uow;
        _motorFiscal    = motorFiscal;
        _configProvider = configProvider;
        _nfeService     = nfeService;
        _tenant         = tenant;
    }

    public async Task<Guid> SalvarRascunhoAsync(SalvarNotaFiscalAvulsaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DestinatarioNome))
            throw new InvalidOperationException("Nome do destinatário é obrigatório.");
        if (!dto.Itens.Any())
            throw new InvalidOperationException("Adicione pelo menos um item.");

        Domain.Entities.NotaFiscal nota;

        if (dto.Id.HasValue)
        {
            nota = await _ctx.NotasFiscais.Include(n => n.Itens)
                .FirstOrDefaultAsync(n => n.Id == dto.Id.Value)
                ?? throw new KeyNotFoundException("Nota não encontrada.");

            if (nota.Status != "Rascunho")
                throw new InvalidOperationException("Só é possível editar uma nota em Rascunho.");

            _ctx.NotaFiscalItens.RemoveRange(nota.Itens);
            nota.Itens.Clear();
        }
        else
        {
            nota = new Domain.Entities.NotaFiscal
            {
                Tipo   = "NFE",
                Status = "Rascunho",
            };
            _ctx.NotasFiscais.Add(nota);
        }

        nota.NaturezaOperacao          = dto.NaturezaOperacao;
        nota.TipoOperacaoEntradaSaida  = dto.TipoOperacaoEntradaSaida;
        nota.DestinatarioNome          = dto.DestinatarioNome;
        nota.DestinatarioDocumento     = dto.DestinatarioDocumento;
        nota.DestinatarioLogradouro    = dto.DestinatarioLogradouro;
        nota.DestinatarioNumero        = dto.DestinatarioNumero;
        nota.DestinatarioBairro        = dto.DestinatarioBairro;
        nota.DestinatarioMunicipio     = dto.DestinatarioMunicipio;
        nota.DestinatarioUf            = dto.DestinatarioUf;
        nota.DestinatarioCep           = dto.DestinatarioCep;
        nota.DestinatarioIe            = dto.DestinatarioIe;

        foreach (var item in dto.Itens)
        {
            nota.Itens.Add(new Domain.Entities.NotaFiscalItem
            {
                ProductId     = item.ProductId,
                ProductName   = item.ProductName,
                Quantidade    = item.Quantidade,
                ValorUnitario = item.ValorUnitario,
                Cfop          = item.Cfop,
            });
        }

        await _ctx.SaveChangesAsync();
        return nota.Id;
    }

    public async Task<NotaFiscalAvulsaDto?> ObterAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null) return null;

        return new NotaFiscalAvulsaDto
        {
            Id                          = nota.Id,
            Status                      = nota.Status,
            UrlDanfe                    = nota.UrlDanfe,
            DataEmissao                 = nota.DataEmissao,
            NaturezaOperacao            = nota.NaturezaOperacao ?? "",
            TipoOperacaoEntradaSaida    = nota.TipoOperacaoEntradaSaida,
            DestinatarioNome            = nota.DestinatarioNome ?? "",
            DestinatarioDocumento       = nota.DestinatarioDocumento,
            DestinatarioLogradouro      = nota.DestinatarioLogradouro,
            DestinatarioNumero          = nota.DestinatarioNumero,
            DestinatarioBairro          = nota.DestinatarioBairro,
            DestinatarioMunicipio       = nota.DestinatarioMunicipio,
            DestinatarioUf              = nota.DestinatarioUf,
            DestinatarioCep             = nota.DestinatarioCep,
            DestinatarioIe              = nota.DestinatarioIe,
            Itens = nota.Itens.Select(i => new NotaFiscalAvulsaItemDto
            {
                ProductId     = i.ProductId,
                ProductName   = i.ProductName,
                Quantidade    = i.Quantidade,
                ValorUnitario = i.ValorUnitario,
                Cfop          = i.Cfop,
            }).ToList(),
        };
    }

    public async Task<IReadOnlyList<NotaFiscalAvulsaResumoDto>> ListarAsync()
    {
        var notas = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .Where(n => n.VendaId == null) // só avulsa — nota de venda não entra aqui
            .OrderByDescending(n => n.DataEmissao)
            .ToListAsync();

        return notas.Select(n => new NotaFiscalAvulsaResumoDto(
            n.Id, n.NaturezaOperacao ?? "", n.DestinatarioNome ?? "",
            n.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
            n.Status, n.DataEmissao)).ToList();
    }

    public async Task ExcluirRascunhoAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != "Rascunho")
            throw new InvalidOperationException("Só é possível excluir uma nota em Rascunho — nota já emitida se cancela, não se exclui.");

        _ctx.NotaFiscalItens.RemoveRange(nota.Itens);
        _ctx.NotasFiscais.Remove(nota);
        await _ctx.SaveChangesAsync();
    }

    public async Task<ConferenciaFiscalDto> ConferirAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        var itensConferencia = new List<ConferenciaItemDto>();
        decimal totalProdutos = 0, totalImpostos = 0;

        foreach (var item in nota.Itens)
        {
            var produto = await _uow.Products.GetByIdAsync(item.ProductId)
                ?? throw new KeyNotFoundException($"Produto '{item.ProductName}' não encontrado.");

            var tributos = _motorFiscal.CalcularTributosVenda(produto, item.Quantidade, item.ValorUnitario);
            var valorTotalItem = item.Quantidade * item.ValorUnitario;

            itensConferencia.Add(new ConferenciaItemDto(item.ProductName, item.Quantidade, item.ValorUnitario, valorTotalItem, tributos));
            totalProdutos += valorTotalItem;
            totalImpostos += tributos.ValorIcms + tributos.ValorIcmsSt;
        }

        return new ConferenciaFiscalDto(itensConferencia, totalProdutos, totalImpostos);
    }

    public async Task<FiscalEmissionResult> EmitirAsync(Guid id)
    {
        var nota = await _ctx.NotasFiscais.Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new KeyNotFoundException("Nota não encontrada.");

        if (nota.Status != "Rascunho")
            throw new InvalidOperationException("Essa nota já foi emitida — não é possível emitir de novo.");

        if (!nota.Itens.Any())
            throw new InvalidOperationException("Nota sem itens.");

        var config = await _configProvider.ObterConfiguracaoAsync();
        string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";

        var itensRequest = new List<FocusItemRequest>();
        foreach (var (item, index) in nota.Itens.Select((it, idx) => (it, idx)))
        {
            var produto = await _uow.Products.GetByIdAsync(item.ProductId);
            itensRequest.Add(new FocusItemRequest
            {
                NumeroItem             = (index + 1).ToString(),
                CodigoProduto          = item.ProductId.ToString().Substring(0, 6),
                Descricao              = item.ProductName,
                QuantidadeComercial    = item.Quantidade.ToString("F2", CultureInfo.InvariantCulture),
                ValorUnitarioComercial = item.ValorUnitario.ToString("F2", CultureInfo.InvariantCulture),
                ValorBruto             = (item.Quantidade * item.ValorUnitario).ToString("F2", CultureInfo.InvariantCulture),
                Cfop                   = item.Cfop,
                CodigoNcm              = string.IsNullOrWhiteSpace(produto?.NCM) ? "00000000" : produto!.NCM!.Replace(".", "").Replace("-", "").Trim(),
                IcmsSituacaoTributaria = string.IsNullOrWhiteSpace(produto?.CSOSN) ? "102" : produto!.CSOSN!.Split('-')[0].Trim(),
                IcmsOrigem             = "0",
                PisSituacaoTributaria     = "99",
                CofinsSituacaoTributaria  = "99",
            });
        }

        string? docLimpo = string.IsNullOrWhiteSpace(nota.DestinatarioDocumento)
            ? null : new string(nota.DestinatarioDocumento.Where(char.IsDigit).ToArray());
        string? cepLimpo = string.IsNullOrWhiteSpace(nota.DestinatarioCep)
            ? null : new string(nota.DestinatarioCep.Where(char.IsDigit).ToArray());

        var request = new FocusNfceRequest
        {
            DataEmissao            = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            TipoDocumento          = "1",
            NaturezaOperacao       = nota.NaturezaOperacao ?? "VENDA DE MERCADORIA",
            FinalidadeEmissao      = nota.Finalidade,
            CpfCnpj                = docLimpo,
            Nome                   = nota.DestinatarioNome,
            LogradouroDestinatario = string.IsNullOrWhiteSpace(nota.DestinatarioLogradouro) ? "Nao Informado" : nota.DestinatarioLogradouro,
            NumeroDestinatario     = string.IsNullOrWhiteSpace(nota.DestinatarioNumero) ? "S/N" : nota.DestinatarioNumero,
            BairroDestinatario     = string.IsNullOrWhiteSpace(nota.DestinatarioBairro) ? "Centro" : nota.DestinatarioBairro,
            MunicipioDestinatario  = string.IsNullOrWhiteSpace(nota.DestinatarioMunicipio) ? "Curitiba" : nota.DestinatarioMunicipio,
            UfDestinatario         = string.IsNullOrWhiteSpace(nota.DestinatarioUf) ? "PR" : nota.DestinatarioUf,
            CepDestinatario        = string.IsNullOrWhiteSpace(cepLimpo) ? "00000000" : cepLimpo,
            IeDestinatario         = nota.DestinatarioIe,
            Itens                  = itensRequest,
            Pagamentos             = new List<FocusPagamentoRequest>(), // nota avulsa não tem pagamento vinculado
        };

        var referencia = $"avulsa-{nota.Id}";
        var (sucesso, mensagem, urlDanfe, urlXml) = await _nfeService.EmitirNfeA4Async(
            referencia, request, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            nota.Status      = "Autorizada";
            nota.UrlDanfe    = urlDanfe;
            nota.XmlUrl      = string.IsNullOrWhiteSpace(urlXml) ? null : urlXml;
            nota.Ambiente    = ambienteSefaz;
            nota.DataEmissao = DateTime.Now;
            await _ctx.SaveChangesAsync();

            return new FiscalEmissionResult
            {
                Sucesso = true, Mensagem = mensagem, Status = "Autorizada",
                UrlDanfe = urlDanfe, Ambiente = ambienteSefaz
            };
        }

        return new FiscalEmissionResult { Sucesso = false, Mensagem = mensagem, Status = "Falha", Ambiente = ambienteSefaz };
    }
}
