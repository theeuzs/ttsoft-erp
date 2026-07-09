// ── ERP.Application/DTOs/FormulaTintometricaDtos.cs ──────────────────────────
using ERP.Domain.Entities;

namespace ERP.Application.DTOs;

public class FormulaDto
{
    public FormulaDto() { }
    public FormulaDto(FormulaTintometrica f)
    {
        Id                   = f.Id;
        ProductId            = f.ProductId;
        ProductName          = f.Product?.Name ?? "";
        SalePrice            = f.Product?.SalePrice ?? 0;
        Fabricante           = f.Fabricante;
        CodigoFabricante     = f.CodigoFabricante;
        NomeCor              = f.NomeCor;
        Base                 = f.Base;
        RendimentoM2PorLitro = f.RendimentoM2PorLitro;
        DemaosRecomendadas   = f.DemaosRecomendadas;
        CorantesJson         = f.CorantesJson;
        Observacoes          = f.Observacoes;
    }

    public Guid    Id                   { get; set; }
    public Guid    ProductId            { get; set; }
    public string  ProductName          { get; set; } = "";
    public decimal SalePrice            { get; set; }
    public string  Fabricante           { get; set; } = "";
    public string  CodigoFabricante     { get; set; } = "";
    public string  NomeCor              { get; set; } = "";
    public string  Base                 { get; set; } = "";
    public decimal RendimentoM2PorLitro { get; set; }
    public int     DemaosRecomendadas   { get; set; }
    public string? CorantesJson         { get; set; }
    public string? Observacoes          { get; set; }
}

public class SalvarFormulaDto
{
    public Guid    ProductId            { get; set; }
    public string  Fabricante           { get; set; } = "";
    public string  CodigoFabricante     { get; set; } = "";
    public string  NomeCor              { get; set; } = "";
    public string  Base                 { get; set; } = "";
    public decimal RendimentoM2PorLitro { get; set; } = 10m;
    public int     DemaosRecomendadas   { get; set; } = 2;
    public string? CorantesJson         { get; set; }
    public string? Observacoes          { get; set; }
}
