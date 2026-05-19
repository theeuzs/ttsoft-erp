// ── ERP.Application/Interfaces/ICalculadoraService.cs ────────────────────────
// I.2: extração da lógica de 605 linhas do CalculadoraController para um service.
// O controller fica com ~40 linhas — só roteamento HTTP.
// ─────────────────────────────────────────────────────────────────────────────
namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de Calculadora de Materiais de Construção.
/// Encapsula os templates, os cálculos e as integrações com estoque/orçamento.
/// </summary>
public interface ICalculadoraService
{
    /// <summary>Lista todos os templates disponíveis com parâmetros de entrada.</summary>
    IReadOnlyList<TemplateObraInfo> GetTemplates();

    /// <summary>
    /// Calcula os materiais para um template dado os parâmetros.
    /// Não requer autenticação — usado pela CalculadoraPublica.razor.
    /// </summary>
    CalcResultado Calcular(string template, Dictionary<string, decimal> parametros);

    /// <summary>
    /// Cruza os materiais calculados com o estoque real do tenant.
    /// Requer autenticação (acessa banco do tenant).
    /// </summary>
    Task<CalcComEstoqueResultado> CalcularComEstoqueAsync(
        string template,
        Dictionary<string, decimal> parametros,
        CancellationToken ct = default);

    /// <summary>
    /// Gera um orçamento completo cruzando materiais com produtos do estoque.
    /// Requer autenticação.
    /// </summary>
    Task<OrcamentoGeradoResultado> GerarOrcamentoAsync(
        string template,
        Dictionary<string, decimal> parametros,
        string? clienteNome,
        Guid?   clienteId,
        CancellationToken ct = default);
}

// ── DTOs de resultado do serviço ──────────────────────────────────────────────

public record TemplateObraInfo(
    string                Id,
    string                Nome,
    string                Descricao,
    string                Icone,
    List<ParametroInfo>   Parametros);

public record ParametroInfo(
    string  Nome,
    string  Label,
    string  Unidade,
    decimal Min,
    decimal Max);

public record MaterialItem(
    string  Nome,
    decimal Quantidade,
    string  Unidade,
    string  Observacao);

public record CalcResultado(
    string             Template,
    string             Nome,
    List<MaterialItem> Materiais,
    int                TotalItens,
    DateTime           GeradoEm);

public record MaterialComEstoque(
    string         Nome,
    decimal        Quantidade,
    string         Unidade,
    string         Observacao,
    EstoqueMatch?  ProdutoEstoque);

public record EstoqueMatch(
    string  Name,
    decimal Stock,
    decimal SalePrice,
    decimal TotalEstimado,
    bool    Suficiente);

public record CalcComEstoqueResultado(
    string                    Template,
    List<MaterialComEstoque>  Materiais,
    decimal                   TotalEstimado,
    DateTime                  GeradoEm);

public record OrcamentoGeradoResultado(
    Guid   OrcamentoId,
    string Numero,
    string Mensagem);
