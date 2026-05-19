// ERP.Application/DTOs/RelatoriosDtos.cs
// DTOs de saída dos serviços de relatório.
// Mantidos simples (sem lógica de apresentação) para respeitar Clean Architecture.

namespace ERP.Application.DTOs;

// ── DRE ──────────────────────────────────────────────────────────────────────
public record DreResultadoDto(
    decimal ReceitaBruta,
    decimal CustoMercadorias,
    decimal LucroBruto,
    decimal DespesasOperacionais,
    decimal LucroLiquido,
    decimal MargemLucratividade);

// ── Curva ABC ─────────────────────────────────────────────────────────────────
public record AbcItemDto(
    string   Nome,
    decimal  Quantidade,
    decimal  TotalFinanceiro);

// ── Comissão ──────────────────────────────────────────────────────────────────
public record ComissaoVendedorDto(
    string  Vendedor,
    int     QtdVendas,
    decimal TotalVendido,
    decimal ValorComissao);

public record ComissaoResultadoDto(
    IReadOnlyList<ComissaoVendedorDto> Vendedores,
    decimal TotalVendidoGeral,
    decimal TotalComissaoPagar);

// ── Margem de Produtos ────────────────────────────────────────────────────────
public record MargemProdutoDto(
    string  Nome,
    string  SKU,
    string  Categoria,
    decimal PrecoVenda,
    decimal PrecoCusto,
    decimal Estoque);

// ── Fluxo de Caixa ────────────────────────────────────────────────────────────
public record FluxoLancamentoDto(
    DateTime Data,
    string   Descricao,
    decimal  Valor,
    string   Tipo,      // "Entrada" | "Saida"
    string   Status);

public record FluxoCaixaResultadoDto(
    IReadOnlyList<FluxoLancamentoDto> Lancamentos,
    decimal TotalEntradas,
    decimal TotalSaidas);

// ── Haver ─────────────────────────────────────────────────────────────────────
public record HaverHistoricoDto(
    DateTime Data,
    string   Tipo,         // "Entrada" | "Saida"
    string   Descricao,
    decimal  Valor,
    string   OperadorNome);

// ── Inventário ────────────────────────────────────────────────────────────────
public record InventarioProdutoDto(
    Guid    ProductId,
    string  Nome,
    string  SKU,
    string  Categoria,
    decimal EstoqueSistema);

// ── BI Avançado ───────────────────────────────────────────────────────────────

public record SazonalidadeDto(
    int    Mes,
    string NomeMes,
    int    Ano,
    decimal ReceitaTotal,
    int    QtdVendas,
    decimal TicketMedio);

public record AbcAvancadoDto(
    string  Nome,
    string  SKU,
    string  Categoria,
    decimal Quantidade,
    decimal TotalFinanceiro,
    decimal PercentualAcumulado,
    string  Classe,           // A, B ou C
    decimal MargemPercent,
    int     Rank);

public record DreDetalhadoDto(
    decimal ReceitaBruta,
    decimal Descontos,
    decimal ReceitaLiquida,
    decimal CustoMercadorias,
    decimal LucroBruto,
    decimal MargemBruta,
    decimal DespesasOperacionais,
    decimal DespesasFixas,
    decimal DespesasVariaveis,
    decimal Ebitda,
    decimal LucroLiquido,
    decimal MargemLiquida,
    IReadOnlyList<DreLinhaDto> LinhasDespesa);

public record DreLinhaDto(string Descricao, decimal Valor, string Categoria);

public record RankingVendedorDto(
    int     Posicao,
    string  Nome,
    int     QtdVendas,
    decimal TotalVendido,
    decimal TicketMedio,
    decimal PercentualTotal);

public record PrevisaoDemandaDto(
    Guid    ProductId,
    string  Nome,
    decimal MediaMensal,
    decimal EstoqueAtual,
    int     DiasEstoque,        // Quantos dias de estoque restam
    bool    AbaixoDoMinimo,
    decimal SugestaoCompra);
