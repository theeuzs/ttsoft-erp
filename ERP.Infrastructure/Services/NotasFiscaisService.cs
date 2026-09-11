// ── ERP.Infrastructure/Services/NotasFiscaisService.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Serviço de consulta de notas fiscais emitidas.
/// Isola o acesso ao AppDbContext que estava direto no NotasFiscaisController.
///
/// NfseEmitida não possui HasQueryFilter no AppDbContext, portanto o filtro
/// de TenantId é aplicado manualmente no WHERE — mesmo padrão do AuditLog.
/// </summary>
public class NotasFiscaisService : INotasFiscaisService
{
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;

    public NotasFiscaisService(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<PagedResult<NotaFiscalDto>> GetAllAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(page, 1);

        var tenantId = _tenant.TenantId;

        // Achado (10/09) — esse endpoint só consultava NfseEmitidas (nota de
        // SERVIÇO). Uma loja de material de construção nunca emite isso —
        // as notas de verdade (NF-e/NFC-e) ficam em NotasFiscais, uma tabela
        // completamente diferente, nunca consultada aqui. Por isso "não
        // aparece nenhuma emitida" mesmo com notas reais no banco.
        //
        // Tentei unir as duas fontes com Concat() ainda como IQueryable
        // (traduzido pro SQL do banco), mas isso esbarrou em duas
        // incompatibilidades reais entre SQL Server (produção) e SQLite
        // (testes): Sum() de decimal em subconsulta, e depois Concat() após
        // um .ToString() de enum na projeção. Em vez de caçar
        // incompatibilidade por incompatibilidade, busca cada fonte
        // separada (com WHERE TenantId, então não é a tabela toda), monta o
        // DTO já em memória (LINQ to Objects — sempre funciona igual em
        // qualquer banco), e combina/pagina em C#. Volume de nota fiscal de
        // uma loja pequena/média nunca chega perto de pesar isso.
        var notasServico = await _ctx.NfseEmitidas
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId)
            .ToListAsync(ct);

        var notasMercadoria = await _ctx.NotasFiscais
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId)
            .ToListAsync(ct);

        // Achado (10/09) — Número, Valor e Tomador sempre vinham vazios/zero
        // na tela. Causa: RegistrarNotaFiscalAsync (FiscalService) nunca
        // grava Numero/Chave na NotaFiscal (só na Sale), e nunca cria
        // NotaFiscalItens (só Nota Avulsa faz isso, que tem gestão de item
        // própria). O dado certo pra nota ligada a venda vive na Sale, não
        // teria como somar item nenhum porque nenhum item foi salvo aqui.
        var idsVenda = notasMercadoria.Where(n => n.VendaId.HasValue).Select(n => n.VendaId!.Value).ToList();
        var vendasPorId = idsVenda.Count == 0
            ? new Dictionary<Guid, (string? Numero, string? Chave, string? Cliente, decimal Total)>()
            : (await _ctx.Sales
                    .AsNoTracking()
                    .Where(s => idsVenda.Contains(s.Id))
                    .Select(s => new { s.Id, s.NfceNumero, s.NfceChave, Nome = s.Customer != null ? s.Customer.Name : null, s.Total })
                    .ToListAsync(ct))
                .ToDictionary(s => s.Id, s => (Numero: s.NfceNumero, Chave: s.NfceChave, Cliente: s.Nome, s.Total));

        var idsNotaFiscal = notasMercadoria.Select(n => n.Id).ToList();
        var totaisPorNota = idsNotaFiscal.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await _ctx.NotaFiscalItens
                    .AsNoTracking()
                    .Where(i => idsNotaFiscal.Contains(i.NotaFiscalId))
                    .Select(i => new { i.NotaFiscalId, i.Quantidade, i.ValorUnitario })
                    .ToListAsync(ct))
                .GroupBy(i => i.NotaFiscalId)
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade * i.ValorUnitario));

        var itensServico = notasServico.Select(n => new NotaFiscalDto
        {
            Id               = n.Id,
            NumeroNfse       = n.NumeroNfse,
            ReferenciaNfse   = n.ReferenciaNfse,
            DataEmissao      = n.DataEmissao,
            Status           = n.Status.ToString(),
            TomadorNome      = n.TomadorNome,
            TomadorCpfCnpj   = n.TomadorCpfCnpj,
            DescricaoServico = n.DescricaoServico,
            ValorServico     = n.ValorServico,
            ValorISS         = n.ValorISS,
            ValorLiquido     = n.ValorLiquido,
            UrlDanfse        = n.UrlDanfse,
            MensagemErro     = n.MensagemErro,
            VendaId          = n.VendaId
        });

        var itensMercadoria = notasMercadoria.Select(n =>
        {
            vendasPorId.TryGetValue(n.VendaId ?? Guid.Empty, out var venda);
            var valorItens = totaisPorNota.TryGetValue(n.Id, out var t) ? t : 0;

            return new NotaFiscalDto
            {
                Id               = n.Id,
                NumeroNfse       = venda.Numero ?? n.Numero,
                ReferenciaNfse   = venda.Chave ?? n.Chave ?? n.RefNFe,
                DataEmissao      = n.DataEmissao,
                Status           = n.Status,
                TomadorNome      = venda.Cliente ?? n.DestinatarioNome ?? "Consumidor Final",
                TomadorCpfCnpj   = n.DestinatarioDocumento,
                DescricaoServico = n.Tipo, // "NFE"/"NFCE" — reaproveita o campo pra diferenciar visualmente
                ValorServico     = 0,
                ValorISS         = 0,
                ValorLiquido     = valorItens > 0 ? valorItens : venda.Total,
                UrlDanfse        = n.UrlDanfe,
                MensagemErro     = n.MotivoCancelamento,
                VendaId          = n.VendaId
            };
        });

        var todas = itensServico.Concat(itensMercadoria)
            .OrderByDescending(n => n.DataEmissao)
            .ToList();

        var total = todas.Count;
        var items = todas.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<NotaFiscalDto>
        {
            Items      = items,
            TotalItems = total,
            Page       = page,
            PageSize   = pageSize
        };
    }
}