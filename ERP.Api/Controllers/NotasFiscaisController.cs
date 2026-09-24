using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Application.DTOs.FocusNfe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/notas-fiscais")]
[Authorize]
public class NotasFiscaisController : ControllerBase
{
    private readonly INotasFiscaisService    _notasService;
    private readonly INfceEmissionService    _nfce;
    private readonly INfeCancellationService _cancel;
    private readonly IFiscalService          _fiscal;
    private readonly IFiscalConfigurationProvider _configProvider;

    public NotasFiscaisController(
        INotasFiscaisService    notasService,
        INfceEmissionService    nfce,
        INfeCancellationService cancel,
        IFiscalService          fiscal,
        IFiscalConfigurationProvider configProvider)
    {
        _notasService   = notasService;
        _nfce           = nfce;
        _cancel         = cancel;
        _fiscal         = fiscal;
        _configProvider = configProvider;
    }

    /// <summary>Lista as notas fiscais emitidas, paginadas por data de emissão decrescente.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pagina = 1,
        [FromQuery] int tam    = 50,
        CancellationToken ct   = default)
        => Ok(await _notasService.GetAllAsync(pagina, tam, ct));

    /// <summary>Emite NFC-e via FocusNFe.</summary>
    [HasPermission(Permissions.NotasFiscaisView)]
    [HttpPost("nfce/emitir")]
    public async Task<IActionResult> EmitirNfce([FromBody] EmitirNfceRequest req)
    {
        // Achado (21/08) — token global do appsettings, ignorava
        // TenantFiscalConfiguration por completo; num cenário com mais de
        // um tenant, todo mundo emitiria com o MESMO token, sempre.
        var config = await _configProvider.ObterConfiguracaoAsync();
        if (string.IsNullOrEmpty(config.TokenFocusNfe))
            return BadRequest(new { erro = "Token FocusNFe não configurado — vá em Configurações → Empresa e Fiscal." });

        var formaPgto = req.FormaPagamento switch
        {
            2 => "03",
            3 => "04",
            4 => "17",
            _ => "01"
        };

        var focusReq = new FocusNfceRequest
        {
            NaturezaOperacao = "Venda ao Consumidor",
            // Achado (21/08) — mesmo bug do S21 (DateTime.Now+zzz depende do
            // fuso AMBIENTE do servidor), nunca corrigido nesse endpoint
            // específico quando foi corrigido em FiscalService.cs.
            DataEmissao      = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasilComOffset(),
            CpfCnpj          = req.CpfCnpjConsumidor,
            Itens            = req.Itens.Select(i => new FocusItemRequest
            {
                NumeroItem               = i.Sequencia.ToString(),
                CodigoProduto            = i.CodigoProduto,
                Descricao                = i.Descricao,
                Cfop                     = "5102",
                UnidadeComercial         = i.Unidade ?? "UN",
                QuantidadeComercial      = i.Quantidade.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                ValorUnitarioComercial   = i.ValorUnitario.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                ValorBruto               = (i.Quantidade * i.ValorUnitario).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                CodigoNcm                = i.Ncm ?? "39269090",
                IcmsOrigem               = "0",
                IcmsSituacaoTributaria   = "400",
                PisSituacaoTributaria    = "99",
                CofinsSituacaoTributaria = "99"
            }).ToList(),
            Pagamentos = [new FocusPagamentoRequest
            {
                FormaPagamento = formaPgto,
                ValorPagamento = req.Total.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            }]
        };

        var referencia = $"venda-{req.VendaId ?? Guid.NewGuid()}";
        var (sucesso, mensagem, urlDanfe, urlXml, chave, numero) = await _nfce.EmitirNfceAsync(
            referencia, focusReq, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (!sucesso)
            return BadRequest(new { erro = mensagem });

        return Ok(new { Sucesso = true, Referencia = referencia, UrlDanfe = urlDanfe, UrlXml = urlXml, Chave = chave, Numero = numero });
    }

    /// <summary>S16 FIX: emite NFC-e ou NF-e a partir de uma venda já persistida —
    /// usado pelo Portal (histórico de notas e PDV web), que chamava essa rota
    /// sem ela existir (404 em produção). Delega para IFiscalService.EmitirNotaAsync,
    /// a mesma lógica já usada pelo WPF (FinalizarVendaViewModel/SaleViewModel) e
    /// pelo OrderProcessingService para pedidos de marketplace.</summary>
    [HasPermission(Permissions.NotasFiscaisView)]
    [HttpPost("{tipo}/emitir-da-venda/{vendaId:guid}")]
    public async Task<IActionResult> EmitirDaVenda(string tipo, Guid vendaId)
    {
        var tipoDocumento = tipo.Equals("nfe", StringComparison.OrdinalIgnoreCase) ? "NFE" : "NFCE";

        FiscalEmissionResult resultado;
        try
        {
            resultado = await _fiscal.EmitirNotaAsync(vendaId, tipoDocumento);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { erro = ex.Message });
        }

        if (!resultado.Sucesso)
            return BadRequest(new { erro = resultado.Mensagem });

        return Ok(new
        {
            Sucesso        = true,
            Mensagem       = resultado.Mensagem,
            Status         = resultado.Status,
            UrlDanfe       = resultado.UrlDanfe,
            Ambiente       = resultado.Ambiente,
            EmContingencia = resultado.EmContingencia
        });
    }

    /// <summary>Cancela uma NFC-e ou NF-e emitida.</summary>
    [HasPermission(Permissions.NotasFiscaisView)]
    [HttpPost("{referencia}/cancelar")]
    public async Task<IActionResult> Cancelar(
        string referencia, [FromBody] CancelarNotaRequest req)
    {
        if (string.IsNullOrEmpty(req.Justificativa) || req.Justificativa.Length < 15)
            return BadRequest(new { erro = "Justificativa deve ter no mínimo 15 caracteres." });

        var config = await _configProvider.ObterConfiguracaoAsync();
        var (sucesso, mensagem) = await _cancel.CancelarNotaAsync(
            referencia, req.Justificativa, config.TokenFocusNfe, config.UsarAmbienteProducao, req.TipoDocumento);

        return sucesso
            ? Ok(new { Sucesso = true, Mensagem = mensagem })
            : BadRequest(new { erro = mensagem });
    }

    /// <summary>Retorna URL de consulta de uma nota pelo número de referência.</summary>
    [HttpGet("{referencia}/status")]
    public async Task<IActionResult> ConsultarStatus(string referencia)
    {
        var config = await _configProvider.ObterConfiguracaoAsync();
        return Ok(new
        {
            Referencia  = referencia,
            UrlConsulta = config.UsarAmbienteProducao
                ? $"https://api.focusnfe.com.br/v2/nfce/{referencia}"
                : $"https://homologacao.focusnfe.com.br/v2/nfce/{referencia}",
            Ambiente = config.UsarAmbienteProducao ? "Produção" : "Homologação"
        });
    }

    /// <summary>Módulo 5 (Fiscal), Etapa 1B — emite NF-e de devolução (finalidade=4),
    /// referenciando a chave da nota original. Chamado por DevolucaoService (WPF),
    /// depois que a devolução operacional (estoque + Haver) já foi commitada —
    /// best-effort de propósito: IFiscalService.EmitirNotaDevolucaoAsync nunca lança
    /// em falha de negócio (sem chave original, rejeição SEFAZ), só em venda
    /// inexistente. Mesmo padrão de resposta do EmitirDaVenda, pra HttpFiscalService
    /// tratar os dois de forma idêntica.</summary>
    [HasPermission(Permissions.NotasFiscaisView)]
    [HttpPost("{vendaId:guid}/emitir-devolucao")]
    public async Task<IActionResult> EmitirDevolucao(Guid vendaId, [FromBody] EmitirDevolucaoRequest req)
    {
        var itens = req.Itens
            .Select(i => (i.ProductId, i.ProductName, i.Quantidade, i.ValorUnitario))
            .ToList();

        FiscalEmissionResult resultado;
        try
        {
            resultado = await _fiscal.EmitirNotaDevolucaoAsync(vendaId, itens, req.Motivo ?? "");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { erro = ex.Message });
        }

        if (!resultado.Sucesso)
            return BadRequest(new { erro = resultado.Mensagem });

        return Ok(new
        {
            Sucesso        = true,
            Mensagem       = resultado.Mensagem,
            Status         = resultado.Status,
            UrlDanfe       = resultado.UrlDanfe,
            Ambiente       = resultado.Ambiente,
            EmContingencia = resultado.EmContingencia
        });
    }
}

public class ItemDevolucaoDto
{
    public Guid    ProductId     { get; set; }
    public string  ProductName   { get; set; } = string.Empty;
    public decimal Quantidade    { get; set; }
    public decimal ValorUnitario { get; set; }
}

public class EmitirDevolucaoRequest
{
    public List<ItemDevolucaoDto> Itens  { get; set; } = [];
    public string?                Motivo { get; set; }
}

public class EmitirNfceRequest
{
    public Guid?   VendaId           { get; set; }
    public int     FormaPagamento    { get; set; } = 1;
    public decimal Total             { get; set; }
    public string? CpfCnpjConsumidor { get; set; }
    public List<ItemNfceRequest> Itens { get; set; } = [];
}

public class ItemNfceRequest
{
    public int     Sequencia     { get; set; }
    public string  CodigoProduto { get; set; } = string.Empty;
    public string  Descricao     { get; set; } = string.Empty;
    public string? Ncm           { get; set; }
    public string? Unidade       { get; set; }
    public decimal Quantidade    { get; set; }
    public decimal ValorUnitario { get; set; }
}

public class CancelarNotaRequest
{
    public string Justificativa { get; set; } = string.Empty;

    /// <summary>"NFCE" (padrão) ou "NFE" — decide qual endpoint da Focus é
    /// usado pra cancelar.</summary>
    public string TipoDocumento { get; set; } = "NFCE";
}