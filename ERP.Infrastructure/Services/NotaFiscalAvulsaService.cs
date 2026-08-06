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
        nota.Finalidade                = dto.Finalidade;
        nota.DestinatarioNome          = dto.DestinatarioNome;
        nota.DestinatarioDocumento     = dto.DestinatarioDocumento;
        nota.DestinatarioLogradouro    = dto.DestinatarioLogradouro;
        nota.DestinatarioNumero        = dto.DestinatarioNumero;
        nota.DestinatarioBairro        = dto.DestinatarioBairro;
        nota.DestinatarioMunicipio     = dto.DestinatarioMunicipio;
        nota.DestinatarioUf            = dto.DestinatarioUf;
        nota.DestinatarioCep           = dto.DestinatarioCep;
        nota.DestinatarioIe            = dto.DestinatarioIe;
        nota.IndicadorIeDestinatario   = dto.IndicadorIeDestinatario;

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

    public async Task<Guid> CopiarComoRascunhoAsync(Guid idOrigem)
    {
        var origem = await _ctx.NotasFiscais.AsNoTracking().Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == idOrigem)
            ?? throw new KeyNotFoundException("Nota original não encontrada.");

        var copia = new Domain.Entities.NotaFiscal
        {
            Tipo                     = "NFE",
            Status                   = "Rascunho",
            NaturezaOperacao         = origem.NaturezaOperacao,
            TipoOperacaoEntradaSaida = origem.TipoOperacaoEntradaSaida,
            Finalidade               = origem.Finalidade,
            DestinatarioNome         = origem.DestinatarioNome,
            DestinatarioDocumento    = origem.DestinatarioDocumento,
            DestinatarioLogradouro   = origem.DestinatarioLogradouro,
            DestinatarioNumero       = origem.DestinatarioNumero,
            DestinatarioBairro       = origem.DestinatarioBairro,
            DestinatarioMunicipio    = origem.DestinatarioMunicipio,
            DestinatarioUf           = origem.DestinatarioUf,
            DestinatarioCep          = origem.DestinatarioCep,
            DestinatarioIe           = origem.DestinatarioIe,
            IndicadorIeDestinatario  = origem.IndicadorIeDestinatario,
        };

        foreach (var item in origem.Itens)
            copia.Itens.Add(new Domain.Entities.NotaFiscalItem
            {
                ProductId     = item.ProductId,
                ProductName   = item.ProductName,
                Quantidade    = item.Quantidade,
                ValorUnitario = item.ValorUnitario,
                Cfop          = item.Cfop,
            });

        _ctx.NotasFiscais.Add(copia);
        await _ctx.SaveChangesAsync();
        return copia.Id;
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
            Finalidade                  = nota.Finalidade,
            DestinatarioNome            = nota.DestinatarioNome ?? "",
            DestinatarioDocumento       = nota.DestinatarioDocumento,
            DestinatarioLogradouro      = nota.DestinatarioLogradouro,
            DestinatarioNumero          = nota.DestinatarioNumero,
            DestinatarioBairro          = nota.DestinatarioBairro,
            DestinatarioMunicipio       = nota.DestinatarioMunicipio,
            DestinatarioUf              = nota.DestinatarioUf,
            DestinatarioCep             = nota.DestinatarioCep,
            DestinatarioIe              = nota.DestinatarioIe,
            IndicadorIeDestinatario     = nota.IndicadorIeDestinatario,
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

        // Fix 2 (plano premium) — endereço inventado autorizado numa NF-e
        // pra CNPJ é pior que rejeição: vira documento fiscal errado em nome
        // de outra empresa. Bloqueia em vez de mandar fallback silencioso.
        string? docLimpoValidacao = string.IsNullOrWhiteSpace(nota.DestinatarioDocumento)
            ? null : new string(nota.DestinatarioDocumento.Where(char.IsDigit).ToArray());
        bool ehCnpj = docLimpoValidacao?.Length == 14;
        if (ehCnpj)
        {
            var faltando = new List<string>();
            if (string.IsNullOrWhiteSpace(nota.DestinatarioLogradouro)) faltando.Add("Logradouro");
            if (string.IsNullOrWhiteSpace(nota.DestinatarioMunicipio)) faltando.Add("Município");
            if (string.IsNullOrWhiteSpace(nota.DestinatarioUf)) faltando.Add("UF");
            if (string.IsNullOrWhiteSpace(nota.DestinatarioCep)) faltando.Add("CEP");
            if (faltando.Any())
                throw new InvalidOperationException(
                    $"Destinatário com CNPJ precisa de endereço completo antes de emitir — faltando: {string.Join(", ", faltando)}.");
        }

        var config = await _configProvider.ObterConfiguracaoAsync();
        string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";

        var itensRequest = new List<FocusItemRequest>();
        decimal valorTotalItens = 0;
        foreach (var (item, index) in nota.Itens.Select((it, idx) => (it, idx)))
        {
            var produto = await _uow.Products.GetByIdAsync(item.ProductId);

            // Fix 6 (plano premium) — NF-e valida NCM de verdade (diferente
            // da NFCe); "00000000" de fallback vai rejeitar. Bloqueia com
            // mensagem clara em vez de deixar a SEFAZ rejeitar depois.
            var ncmLimpo = produto?.NCM?.Replace(".", "").Replace("-", "").Trim();
            if (string.IsNullOrWhiteSpace(ncmLimpo) || ncmLimpo.Length != 8)
                throw new InvalidOperationException(
                    $"Produto '{item.ProductName}' está sem NCM válido — edite o cadastro do produto antes de emitir.");

            // Fix 4 (plano premium) — GUID truncado como código no DANFE é
            // sem significado e arrisca colisão. Usa SKU de verdade.
            string codigoProduto = !string.IsNullOrWhiteSpace(produto?.SKU) ? produto!.SKU!
                : !string.IsNullOrWhiteSpace(produto?.Barcode) ? produto!.Barcode!
                : item.ProductId.ToString()[..6];

            itensRequest.Add(new FocusItemRequest
            {
                NumeroItem             = (index + 1).ToString(),
                CodigoProduto          = codigoProduto,
                Descricao              = item.ProductName,
                QuantidadeComercial    = item.Quantidade.ToString("F2", CultureInfo.InvariantCulture),
                ValorUnitarioComercial = item.ValorUnitario.ToString("F2", CultureInfo.InvariantCulture),
                ValorBruto             = (item.Quantidade * item.ValorUnitario).ToString("F2", CultureInfo.InvariantCulture),
                Cfop                   = item.Cfop,
                CodigoNcm              = ncmLimpo,
                IcmsSituacaoTributaria = string.IsNullOrWhiteSpace(produto?.CSOSN) ? "102" : produto!.CSOSN!.Split('-')[0].Trim(),
                IcmsOrigem             = "0",
                PisSituacaoTributaria     = "99",
                CofinsSituacaoTributaria  = "99",
            });
            valorTotalItens += item.Quantidade * item.ValorUnitario;
        }

        string? docLimpo = docLimpoValidacao;
        string? cepLimpo = string.IsNullOrWhiteSpace(nota.DestinatarioCep)
            ? null : new string(nota.DestinatarioCep.Where(char.IsDigit).ToArray());

        var request = new FocusNfceRequest
        {
            DataEmissao            = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            // Fix 1 (plano premium) — antes hardcoded "1" (saída), então toda
            // nota de entrada/devolução-de-venda saía errada na SEFAZ.
            TipoDocumento          = nota.TipoOperacaoEntradaSaida == "E" ? "0" : "1",
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
            // Fix 3 (plano premium) — confirmado na doc da Focus
            // (indicador_inscricao_estadual_destinatario); sem isso, NF-e
            // B2B toma rejeição clássica dependendo do destinatário.
            IndicadorIeDestinatario = nota.IndicadorIeDestinatario,
            Itens                  = itensRequest,
            // Fix 5 (plano premium) — NF-e exige a tag de pagamento; nota
            // avulsa sem cobrança (remessa, brinde, devolução) precisa ir
            // explicitamente como forma "90 — Sem pagamento", não vazio.
            Pagamentos = new List<FocusPagamentoRequest>
            {
                new() { FormaPagamento = "90", ValorPagamento = valorTotalItens.ToString("F2", CultureInfo.InvariantCulture) }
            },
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