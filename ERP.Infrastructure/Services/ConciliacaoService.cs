// ── ERP.Infrastructure/Services/ConciliacaoService.cs ────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ERP.Infrastructure.Services;

public class ConciliacaoService : IConciliacaoService
{
    private readonly AppDbContext _ctx;
    public ConciliacaoService(AppDbContext ctx) => _ctx = ctx;

    // S9: limite de linhas — evita DoS por arquivo CSV gigante.
    private const int MaxLinhas = 5_000;

    public async Task<ConciliacaoResultadoDto> ImportarExtratoAsync(
        Stream csvStream, string? separador, CancellationToken ct = default)
    {
        // Parse CSV
        var linhas = new List<string[]>();
        var sep = (separador ?? ";")[0];

        using var reader = new StreamReader(csvStream);
        string? cabecalho = null;
        while (!reader.EndOfStream)
        {
            var linha = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(linha)) continue;
            if (cabecalho is null) { cabecalho = linha; continue; }

            // S9: cap de linhas
            if (linhas.Count >= MaxLinhas)
                throw new InvalidOperationException(
                    $"Extrato muito grande. Máximo: {MaxLinhas} linhas por importação.");

            // S10 FIX: parser RFC 4180 — trata campos com aspas e vírgulas internas.
            // Antes: linha.Split(sep) quebrava em "R$ 1.234,56" quando sep=','.
            linhas.Add(ParseCsvLine(linha, sep));
        }

        if (cabecalho is null || !linhas.Any())
            throw new InvalidOperationException("CSV vazio ou sem dados.");

        // Detecta colunas por nome no cabeçalho
        var cols = ParseCsvLine(cabecalho, sep).Select(c => c.Trim().ToLower()).ToArray();
        int idxData  = FindCol(cols, "data", "date", "dt");
        int idxValor = FindCol(cols, "valor", "value", "amount", "vl");
        int idxDesc  = FindCol(cols, "descricao", "descricão", "estabelecimento", "description", "historico");

        if (idxData < 0 || idxValor < 0)
            throw new InvalidOperationException(
                $"CSV deve ter colunas Data e Valor. Colunas encontradas: {cabecalho}");

        // Extrai transações do extrato
        var transacoes = linhas
            .Select(l =>
            {
                if (!DateTime.TryParse(l.ElementAtOrDefault(idxData), out var data)) return null;
                var valorStr = l.ElementAtOrDefault(idxValor)?.Replace("R$", "").Replace(".", "").Replace(",", ".");
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
            throw new InvalidOperationException("Nenhuma transação válida encontrada no CSV.");

        var inicio = transacoes.Min(t => t.Data).Date;
        var fim    = transacoes.Max(t => t.Data).Date.AddDays(1);

        // Busca vendas no período com cartão
        var vendas = await _ctx.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= inicio && s.SaleDate < fim
                     && s.Status != ERP.Domain.Enums.SaleStatus.Cancelada)
            .Select(s => new
            {
                s.Id, s.SaleNumber, s.SaleDate, s.Total, s.SellerName
            })
            .ToListAsync(ct);

        // Cruzamento: tenta casar por data + valor (tolerância de R$0.01)
        var resultado = transacoes.Select(t =>
        {
            var venda = vendas.FirstOrDefault(v =>
                v.SaleDate.Date == t.Data.Date &&
                Math.Abs(v.Total - t.Valor) < 0.02m);

            return new ItemConciliacaoDto(
                DataExtrato:   t.Data,
                ValorExtrato:  t.Valor,
                DescExtrato:   t.Descricao,
                Conciliado:    venda is not null,
                NumeroVenda:   venda?.SaleNumber,
                VendaId:       venda?.Id,
                ValorVenda:    venda?.Total,
                VendedorVenda: venda?.SellerName,
                Diferenca:     venda is null ? null : (decimal?)(t.Valor - venda.Total));
        }).ToList();

        return new ConciliacaoResultadoDto(
            TotalLinhas:     transacoes.Count,
            Conciliados:     resultado.Count(r => r.Conciliado),
            NaoConciliados:  resultado.Count(r => !r.Conciliado),
            TotalExtrato:    transacoes.Sum(t => t.Valor),
            TotalConciliado: resultado.Where(r => r.Conciliado).Sum(r => r.ValorExtrato),
            Inicio:          inicio,
            Fim:             fim.AddDays(-1),
            Itens:           resultado);
    }

    // S9: neutraliza formula injection — =SUM(...), -1+1, @SUM, +cmd|' /C calc'!A0 etc.
    // Prefixar com ' faz o Excel/LibreOffice tratar como texto literal.
    private static string Sanitize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return "=-+@\t".Contains(s[0]) ? "'" + s : s;
    }

    // S10: Parser RFC 4180 — trata campos entre aspas com delimitadores internos.
    // Ex: "R$ 1.234,56";"Loja ""Silva"" Ltda" → ["R$ 1.234,56", "Loja \"Silva\" Ltda"]
    private static string[] ParseCsvLine(string line, char sep)
    {
        var fields  = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Aspas duplas dentro de campo quoted → aspas literais
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false; // fecha o campo quoted
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == sep) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static int FindCol(string[] cols, params string[] candidatos)
        => Array.FindIndex(cols, c => candidatos.Any(k => c.Contains(k)));

    private class TransacaoExtrato
    {
        public DateTime Data      { get; set; }
        public decimal  Valor     { get; set; }
        public string   Descricao { get; set; } = "";
    }
}
