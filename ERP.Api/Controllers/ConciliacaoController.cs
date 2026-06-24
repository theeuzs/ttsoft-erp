using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConciliacaoController : ControllerBase
{
    private readonly AppDbContext _db;
    public ConciliacaoController(AppDbContext db) => _db = db;

    // S9: limite de 2 MB no upload — evita DoS por arquivo CSV gigante.
    private const int MaxLinhas = 5_000;

    /// <summary>
    /// Recebe CSV do extrato da operadora de cartão e cruza com vendas do período.
    /// Suporta formato genérico com colunas: Data, Valor, Estabelecimento/Descrição.
    /// Formatos testados: Cielo, Rede, Stone, GetNet (CSV padrão).
    /// </summary>
    [HasPermission(Permissions.FinanceiroView)]
    [RequestSizeLimit(2_097_152)] // S9: 2 MB — evita DoS por upload gigante
    [HttpPost("importar-extrato")]
    public async Task<IActionResult> ImportarExtrato(
        [FromForm] IFormFile arquivo,
        [FromQuery] string? separador = null)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { erro = "Arquivo CSV não enviado." });

        if (!arquivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { erro = "Apenas arquivos .csv são aceitos." });

        // Parse CSV
        var linhas = new List<string[]>();
        var sep = (separador ?? ";")[0];

        using var reader = new System.IO.StreamReader(arquivo.OpenReadStream());
        string? cabecalho = null;
        while (!reader.EndOfStream)
        {
            var linha = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(linha)) continue;
            if (cabecalho is null) { cabecalho = linha; continue; }

            // S9: cap de linhas — impede CSV com milhões de linhas consumindo toda a RAM/CPU.
            if (linhas.Count >= MaxLinhas)
                return BadRequest(new { erro = $"Extrato muito grande. Máximo: {MaxLinhas} linhas por importação." });

            linhas.Add(linha.Split(sep));
        }

        if (cabecalho is null || !linhas.Any())
            return BadRequest(new { erro = "CSV vazio ou sem dados." });

        // Detecta colunas por nome no cabeçalho
        var cols = cabecalho.Split(sep).Select(c => c.Trim().ToLower()).ToArray();
        int idxData  = FindCol(cols, "data", "date", "dt");
        int idxValor = FindCol(cols, "valor", "value", "amount", "vl");
        int idxDesc  = FindCol(cols, "descricao", "descricão", "estabelecimento", "description", "historico");

        if (idxData < 0 || idxValor < 0)
            return BadRequest(new { erro = $"CSV deve ter colunas Data e Valor. Colunas encontradas: {cabecalho}" });

        // Extrai transações do extrato
        var transacoes = linhas
            .Select(l =>
            {
                if (!DateTime.TryParse(l.ElementAtOrDefault(idxData), out var data)) return null;
                var valorStr = l.ElementAtOrDefault(idxValor)?.Replace("R$","").Replace(".","").Replace(",",".");
                if (!decimal.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor)) return null;
                if (valor <= 0) return null;

                return new TransacaoExtrato
                {
                    Data      = data,
                    Valor     = valor,
                    // S9: sanitização anti-formula injection — células que começam com = - + @
                    // são prefixadas com ' para neutralizar execução em Excel/LibreOffice.
                    Descricao = Sanitize(l.ElementAtOrDefault(idxDesc))
                };
            })
            .Where(t => t is not null)
            .Cast<TransacaoExtrato>()
            .ToList();

        if (!transacoes.Any())
            return BadRequest(new { erro = "Nenhuma transação válida encontrada no CSV." });

        var inicio = transacoes.Min(t => t.Data).Date;
        var fim    = transacoes.Max(t => t.Data).Date.AddDays(1);

        // Busca vendas no período com cartão
        var vendas = await _db.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= inicio && s.SaleDate < fim
                     && s.Status != ERP.Domain.Enums.SaleStatus.Cancelada)
            .Select(s => new
            {
                s.Id, s.SaleNumber, s.SaleDate, s.Total, s.SellerName
            })
            .ToListAsync();

        // Cruzamento: tenta casar por data + valor (tolerância de R$0.01)
        var resultado = transacoes.Select(t =>
        {
            var venda = vendas.FirstOrDefault(v =>
                v.SaleDate.Date == t.Data.Date &&
                Math.Abs(v.Total - t.Valor) < 0.02m);

            return new ItemConciliacao
            {
                DataExtrato   = t.Data,
                ValorExtrato  = t.Valor,
                DescExtrato   = t.Descricao,
                Conciliado    = venda is not null,
                NumeroVenda   = venda?.SaleNumber,
                VendaId       = venda?.Id,
                ValorVenda    = venda?.Total,
                VendedorVenda = venda?.SellerName,
                Diferenca     = venda is null ? null : (decimal?)(t.Valor - venda.Total)
            };
        }).ToList();

        return Ok(new
        {
            TotalLinhas     = transacoes.Count,
            Conciliados     = resultado.Count(r => r.Conciliado),
            NaoConciliados  = resultado.Count(r => !r.Conciliado),
            TotalExtrato    = transacoes.Sum(t => t.Valor),
            TotalConciliado = resultado.Where(r => r.Conciliado).Sum(r => r.ValorExtrato),
            Periodo         = new { Inicio = inicio, Fim = fim.AddDays(-1) },
            Itens           = resultado
        });
    }

    // S9: neutraliza formula injection — =SUM(...), -1+1, @SUM, +cmd|' /C calc'!A0 etc.
    // Prefixar com ' faz o Excel/LibreOffice tratar como texto literal.
    private static string Sanitize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return "=-+@\t".Contains(s[0]) ? "'" + s : s;
    }

    private static int FindCol(string[] cols, params string[] candidatos)
        => Array.FindIndex(cols, c => candidatos.Any(k => c.Contains(k)));
}

internal class TransacaoExtrato
{
    public DateTime Data      { get; set; }
    public decimal  Valor     { get; set; }
    public string   Descricao { get; set; } = "";
}

internal class ItemConciliacao
{
    public DateTime  DataExtrato   { get; set; }
    public decimal   ValorExtrato  { get; set; }
    public string    DescExtrato   { get; set; } = "";
    public bool      Conciliado    { get; set; }
    public string?   NumeroVenda   { get; set; }
    public Guid?     VendaId       { get; set; }
    public decimal?  ValorVenda    { get; set; }
    public string?   VendedorVenda { get; set; }
    public decimal?  Diferenca     { get; set; }
}