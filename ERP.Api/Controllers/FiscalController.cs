using ERP.Application.Interfaces;
using ERP.Domain.Services.Fiscal;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FiscalController : ControllerBase
{
    private readonly ICMSSTCalculator             _st;
    private readonly INfseEmissionService         _nfse;
    private readonly SpedEfdGenerator             _sped;
    private readonly SpedContribuicoesGenerator   _spedContrib;
    private readonly IConfiguration               _config;

    public FiscalController(
        ICMSSTCalculator st,
        INfseEmissionService nfse,
        SpedEfdGenerator sped,
        SpedContribuicoesGenerator spedContrib,
        IConfiguration config)
    {
        _st          = st;
        _nfse        = nfse;
        _sped        = sped;
        _spedContrib = spedContrib;
        _config      = config;
    }

    // ── ICMS-ST ──────────────────────────────────────────────────────────────

    /// <summary>Calcula ICMS-ST para um item com os parâmetros informados.</summary>
    [HttpPost("icms-st/calcular")]
    public IActionResult CalcularST([FromBody] CalcularSTRequest req)
    {
        var result = _st.Calcular(
            req.ValorProduto,
            req.AliquotaInternaUFDest,
            req.MVAOriginal,
            req.AliquotaInterestadual,
            req.AliquotaIcmsOrigem);

        return Ok(new
        {
            result.BaseCalculoST,
            result.ValorICMSST,
            result.MVAUtilizado,
            result.IsInterestadual,
            TotalComST = req.ValorProduto + result.ValorICMSST
        });
    }

    // ── NFS-e ─────────────────────────────────────────────────────────────────

    /// <summary>Emite uma NFS-e via FocusNFe.</summary>
    [HttpPost("nfse/emitir")]
    public async Task<IActionResult> EmitirNfse([FromBody] EmitirNfseDto dto)
    {
        var token     = _config["FocusNfe:Token"] ?? string.Empty;
        var producao  = bool.Parse(_config["FocusNfe:IsProducao"] ?? "false");

        var (sucesso, msg, nfse) = await _nfse.EmitirAsync(dto, token, producao);

        if (!sucesso)
            return BadRequest(new { erro = msg, referencia = nfse?.ReferenciaNfse });

        return Ok(new
        {
            nfse!.NumeroNfse,
            nfse.ReferenciaNfse,
            nfse.ValorServico,
            nfse.ValorISS,
            nfse.UrlDanfse,
            nfse.CodigoVerificacao
        });
    }

    /// <summary>Cancela uma NFS-e emitida.</summary>
    [HttpDelete("nfse/{referencia}/cancelar")]
    public async Task<IActionResult> CancelarNfse(string referencia, [FromBody] string motivo)
    {
        var token    = _config["FocusNfe:Token"] ?? string.Empty;
        var producao = bool.Parse(_config["FocusNfe:IsProducao"] ?? "false");

        var (sucesso, msg) = await _nfse.CancelarAsync(referencia, motivo, token, producao);
        return sucesso ? NoContent() : BadRequest(new { erro = msg });
    }

    // ── SPED ──────────────────────────────────────────────────────────────────

    /// <summary>Gera arquivo SPED EFD para download.</summary>
    [HttpGet("sped/gerar")]
    public IActionResult GerarSped(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim,
        [FromQuery] string razaoSocial,
        [FromQuery] string cnpj,
        [FromQuery] string ie = "",
        [FromQuery] string codigoMunicipio = "4106902")
    {
        var gen = new SpedEfdGenerator();

        gen.GerarBloco0(new SpedConfig
        {
            DataInicio      = dataInicio,
            DataFim         = dataFim,
            RazaoSocial     = razaoSocial,
            CNPJ            = cnpj,
            IE              = ie,
            CodigoMunicipio = codigoMunicipio,
            IndPerfil       = "C",
            IndAtividade    = "0"
        });

        gen.IniciarBlocoC();
        gen.EncerrarBlocoC();
        gen.GerarBlocoH(Array.Empty<SpedItemInventario>(),
            dataFim.ToString("ddMMyyyy"));

        var conteudo  = gen.Encerrar();
        var nomeArq   = $"EFD_{cnpj}_{dataInicio:yyyyMM}.txt";
        var bytes     = System.Text.Encoding.UTF8.GetBytes(conteudo);

        return File(bytes, "text/plain", nomeArq);
    }


    /// <summary>Calcula carga tributária completa de um produto para uma UF destino.</summary>
    [HttpPost("calcular-impostos")]
    public IActionResult CalcularImpostos(
        [FromBody] ERP.Infrastructure.Services.ProdutoFiscal produto,
        [FromQuery] string ufDestino = "PR")
    {
        var resultado = ERP.Infrastructure.Services.MotorFiscalBrasileiro
            .CalcularImpostos(produto, ufDestino);
        return Ok(resultado);
    }

    /// <summary>Retorna alíquota interna de ICMS de uma UF.</summary>
    [HttpGet("aliquota-icms/{uf}")]
    public IActionResult AliquotaICMS(string uf)
        => Ok(new { UF = uf.ToUpper(),
            AliquotaInterna = ERP.Infrastructure.Services.MotorFiscalBrasileiro
                .ObterAliquotaInterna(uf) });

    /// <summary>CFOP automático baseado em UF origem/destino.</summary>
    [HttpGet("cfop")]
    public IActionResult ObterCFOP(
        [FromQuery] string ufOrigem, [FromQuery] string ufDestino,
        [FromQuery] bool servico = false)
        => Ok(new { CFOP = ERP.Infrastructure.Services.MotorFiscalBrasileiro
            .ObterCFOP(ufOrigem, ufDestino, servico) });


    /// <summary>Gera EFD-Contribuições (PIS/COFINS) para download.</summary>
    [HttpGet("sped-contrib/gerar")]
    public IActionResult GerarSpedContrib(
        [FromQuery] DateTime dataInicio,
        [FromQuery] DateTime dataFim,
        [FromQuery] string   razaoSocial,
        [FromQuery] string   cnpj,
        [FromQuery] string   incidencia = "1")
    {
        var gen = new SpedContribuicoesGenerator();
        gen.GerarBloco0(new SpedConfig
        {
            DataInicio      = dataInicio,
            DataFim         = dataFim,
            RazaoSocial     = razaoSocial,
            CNPJ            = cnpj,
            CodigoMunicipio = "4106902",
            IndAtividade    = "0"
        }, incidencia);

        gen.IniciarBlocoA(cnpj);
        gen.EncerrarBlocoA();
        gen.IniciarBlocoC(cnpj);
        gen.EncerrarBlocoC();
        gen.GerarBlocoF(cnpj, 0m);
        gen.GerarBloco1(0m, 0m);

        var conteudo = gen.Encerrar();
        var nomeArq  = $"EFDContrib_{cnpj}_{dataInicio:yyyyMM}.txt";
        return File(System.Text.Encoding.UTF8.GetBytes(conteudo), "text/plain", nomeArq);
    }
}

public class CalcularSTRequest
{
    public decimal ValorProduto          { get; set; }
    public decimal AliquotaInternaUFDest { get; set; }
    public decimal MVAOriginal           { get; set; }
    public decimal AliquotaInterestadual { get; set; } = 0m;
    public decimal AliquotaIcmsOrigem    { get; set; } = 0m;
}
