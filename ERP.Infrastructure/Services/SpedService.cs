using System;
using System.Linq;
using System.Threading.Tasks;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Services;

public class SpedService : ISpedService
{
    private readonly IServiceProvider _sp;

    public SpedService(IServiceProvider sp) => _sp = sp;

    public async Task<string> GerarEfdIcmsAsync(SpedParametros p)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Busca todas as vendas com NF-e emitida no período, com seus itens e produtos
        var vendas = await ctx.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= p.DataInicio.Date
                     && s.SaleDate <= p.DataFim.Date.AddDays(1).AddTicks(-1)
                     && s.Status == SaleStatus.NotaEmitida
                     && !s.IsDeleted)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .Include(s => s.Customer)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        // Busca estoque atual para o Bloco H (inventário)
        var produtos = await ctx.Products
            .AsNoTracking()
            .Where(p2 => p2.IsActive && !p2.IsDeleted)
            .ToListAsync();

        var gen = new SpedEfdGenerator();

        // ── Bloco 0 ────────────────────────────────────────────────────────────
        gen.GerarBloco0(new SpedConfig
        {
            DataInicio      = p.DataInicio,
            DataFim         = p.DataFim,
            RazaoSocial     = p.RazaoSocial,
            CNPJ            = LimparDoc(p.CNPJ),
            IE              = p.IE,
            CodigoMunicipio = p.CodigoMunicipio,
            IM              = p.IM,
            SUFRAMA         = "",
            IndPerfil       = p.IndPerfil,
            IndAtividade    = "0",
            Endereco        = p.Endereco,
            ContabNome      = p.ContabNome,
            ContabCpf       = LimparDoc(p.ContabCpf),
            ContabCrc       = p.ContabCrc,
            ContabEmail     = p.ContabEmail,
            ContabFone      = p.ContabFone
        });

        // ── Bloco C — Notas Fiscais emitidas ──────────────────────────────────
        gen.IniciarBlocoC();

        foreach (var venda in vendas)
        {
            var nota = new SpedNota
            {
                IndOper      = "1",   // Saída
                IndEmit      = "0",   // Próprio
                CodModelo    = venda.Items.Count > 0 ? "65" : "55", // NFC-e ou NF-e
                CodSit       = "00",  // Normal
                NumDoc       = venda.SaleNumber.PadLeft(9, '0'),
                Serie        = "1",
                DataEmissao  = venda.SaleDate,
                ValorTotal   = venda.Total,
                ValorDesconto = venda.Items.Sum(i => i.Quantity * i.UnitPrice * i.DiscountPercent / 100),
                ValorIcms    = 0, // Simples Nacional — ICMS recolhido pelo DAS
                ValorIpi     = 0,
                ValorPis     = 0,
                ValorCofins  = 0,
                ChaveAcesso  = "", // preenchido quando integrado ao Focus
                Itens        = venda.Items.Select((item, idx) => new SpedItemNota
                {
                    NumItem       = idx + 1,
                    CodItem       = item.Product?.SKU ?? item.ProductId.ToString()[..8],
                    Descricao     = item.ProductName,
                    Quantidade    = item.Quantity,
                    Unidade       = item.Product?.Unit ?? "UN",
                    ValorTotal    = item.TotalPrice,
                    ValorDesconto = item.Quantity * item.UnitPrice * item.DiscountPercent / 100,
                    IndMov        = "0",
                    CstIcms       = item.Product?.CSOSN ?? "400", // 400=Simples sem permissão de crédito
                    CFOP          = item.Product?.CFOPPadrao ?? "5102",
                    CodNat        = "001",
                    ValorBaseIcms = 0,
                    AliqIcms      = 0,
                    ValorIcms     = 0
                }).ToList()
            };

            gen.AdicionarNota(nota);
        }

        gen.EncerrarBlocoC();

        // ── Bloco H — Inventário (estoque atual na data fim) ───────────────────
        var itensInventario = produtos
            .Where(p2 => p2.Stock > 0)
            .Select(p2 => new SpedItemInventario
            {
                CodItem       = p2.SKU ?? p2.Id.ToString()[..8],
                Unidade       = p2.Unit ?? "UN",
                Quantidade    = p2.Stock,
                ValorUnitario = p2.CostPrice > 0 ? p2.CostPrice : p2.SalePrice,
                IndProp       = "0",
                VlCustoPadrao = p2.CostPrice
            });

        gen.GerarBlocoH(itensInventario, p.DataFim.ToString("ddMMyyyy"));

        return gen.Encerrar();
    }

    public async Task<string> GerarEfdContribuicoesAsync(SpedParametros p)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vendas = await ctx.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= p.DataInicio.Date
                     && s.SaleDate <= p.DataFim.Date.AddDays(1).AddTicks(-1)
                     && s.Status == SaleStatus.NotaEmitida
                     && !s.IsDeleted)
            .Include(s => s.Items)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        var gen = new SpedContribuicoesGenerator();

        gen.GerarBloco0(new SpedConfig
        {
            DataInicio      = p.DataInicio,
            DataFim         = p.DataFim,
            RazaoSocial     = p.RazaoSocial,
            CNPJ            = LimparDoc(p.CNPJ),
            IE              = p.IE,
            CodigoMunicipio = p.CodigoMunicipio,
            IndAtividade    = "0",
            Endereco        = p.Endereco,
            ContabNome      = p.ContabNome,
            ContabCpf       = LimparDoc(p.ContabCpf),
            ContabCrc       = p.ContabCrc,
            ContabEmail     = p.ContabEmail,
            ContabFone      = p.ContabFone
        }, "1"); // 1 = Cumulativo (Simples Nacional)

        var cnpj = LimparDoc(p.CNPJ);

        // Bloco A — Serviços (vazio se não houver NFS-e)
        gen.IniciarBlocoA(cnpj);
        gen.EncerrarBlocoA();

        // Bloco C — Produtos
        gen.IniciarBlocoC(cnpj);
        foreach (var venda in vendas)
        {
            gen.AdicionarNotaProduto(new SpedNotaContrib
            {
                IndOper      = "1",
                IndEmit      = "0",
                CodModelo    = "65",
                CodSit       = "00",
                NumDoc       = venda.SaleNumber.PadLeft(9, '0'),
                Serie        = "1",
                DataEmissao  = venda.SaleDate,
                ValorTotal   = venda.Total,
                ValorPis     = 0, // Simples — PIS recolhido pelo DAS
                ValorCofins  = 0,
                Itens = venda.Items.Select((item, idx) => new SpedItemContrib
                {
                    NumItem    = idx + 1,
                    Descricao  = item.ProductName,
                    ValorTotal = item.TotalPrice,
                    CstPis      = "99", // 99=Outras operações (Simples Nacional)
                    CstCofins   = "99", // 99=Outras operações (Simples Nacional)
                    AliqPis    = 0,
                    ValorPis   = 0,
                    AliqCofins = 0,
                    ValorCofins = 0
                }).ToList()
            });
        }
        gen.EncerrarBlocoC();

        // Bloco F — Demais receitas (zerado)
        gen.GerarBlocoF(cnpj, 0m);

        // Bloco 1 — Complementar (zerado)
        gen.GerarBloco1(0m, 0m);

        return gen.Encerrar();
    }

    private static string LimparDoc(string s) =>
        new string(s.Where(char.IsDigit).ToArray());
}
