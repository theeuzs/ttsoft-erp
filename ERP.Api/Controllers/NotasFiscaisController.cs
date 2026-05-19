using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Application.DTOs.FocusNfe;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/notas-fiscais")]
[Authorize]
public class NotasFiscaisController : ControllerBase
{
    private readonly INotasFiscaisService    _notasService;
    private readonly INfceEmissionService    _nfce;
    private readonly INfeCancellationService _cancel;
    private readonly IConfiguration         _config;

    public NotasFiscaisController(
        INotasFiscaisService    notasService,
        INfceEmissionService    nfce,
        INfeCancellationService cancel,
        IConfiguration          config)
    {
        _notasService = notasService;
        _nfce         = nfce;
        _cancel       = cancel;
        _config       = config;
    }

    private string Token      => _config["FocusNfe:Token"] ?? "";
    private bool   IsProducao => _config.GetValue<bool>("FocusNfe:IsProducao");

    /// <summary>Lista as notas fiscais emitidas, paginadas por data de emissão decrescente.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pagina = 1,
        [FromQuery] int tam    = 50,
        CancellationToken ct   = default)
        => Ok(await _notasService.GetAllAsync(pagina, tam, ct));

    /// <summary>Emite NFC-e via FocusNFe.</summary>
    [HttpPost("nfce/emitir")]
    public async Task<IActionResult> EmitirNfce([FromBody] EmitirNfceRequest req)
    {
        if (string.IsNullOrEmpty(Token))
            return BadRequest(new { erro = "Token FocusNFe não configurado em FocusNfe:Token." });

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
            DataEmissao      = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),
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
        var (sucesso, mensagem, urlDanfe) = await _nfce.EmitirNfceAsync(
            referencia, focusReq, Token, IsProducao);

        if (!sucesso)
            return BadRequest(new { erro = mensagem });

        return Ok(new { Sucesso = true, Referencia = referencia, UrlDanfe = urlDanfe });
    }

    /// <summary>Cancela uma NFC-e ou NF-e emitida.</summary>
    [HttpPost("{referencia}/cancelar")]
    public async Task<IActionResult> Cancelar(
        string referencia, [FromBody] CancelarNotaRequest req)
    {
        if (string.IsNullOrEmpty(req.Justificativa) || req.Justificativa.Length < 15)
            return BadRequest(new { erro = "Justificativa deve ter no mínimo 15 caracteres." });

        var (sucesso, mensagem) = await _cancel.CancelarNotaAsync(
            referencia, req.Justificativa, Token, IsProducao);

        return sucesso
            ? Ok(new { Sucesso = true, Mensagem = mensagem })
            : BadRequest(new { erro = mensagem });
    }

    /// <summary>Retorna URL de consulta de uma nota pelo número de referência.</summary>
    [HttpGet("{referencia}/status")]
    public IActionResult ConsultarStatus(string referencia)
        => Ok(new
        {
            Referencia  = referencia,
            UrlConsulta = IsProducao
                ? $"https://api.focusnfe.com.br/v2/nfce/{referencia}"
                : $"https://homologacao.focusnfe.com.br/v2/nfce/{referencia}",
            Ambiente = IsProducao ? "Produção" : "Homologação"
        });
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
}