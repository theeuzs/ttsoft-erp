// ── ERP.Application/DTOs/VendaSuspensaDtos.cs ─────────────────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

/// <summary>Linha da grid de vendas suspensas pendentes.</summary>
public class VendaSuspensaResumoDto
{
    public Guid     Id             { get; set; }
    public DateTime DataSuspensao  { get; set; }
    public string   ClienteNome    { get; set; } = string.Empty;
    public int      QuantidadeItens { get; set; }
    public decimal  TotalAproximado { get; set; }
    public string   NomeSuspensor  { get; set; } = string.Empty;

    public bool      EmEdicao        { get; set; }
    public string?    NomeEmEdicao   { get; set; }
    public DateTime?  DataInicioEdicao { get; set; }
}

public class VendaSuspensaItemDto
{
    public Guid    ProductId         { get; set; }
    public string  ProductName       { get; set; } = string.Empty;
    public decimal Quantity          { get; set; }
    public decimal NormalUnitPrice   { get; set; }
    public decimal UnitPrice         { get; set; }
    public string  Observacao        { get; set; } = string.Empty;
    public decimal FatorConversao    { get; set; } = 1;
    public string  UnidadeEstoque    { get; set; } = string.Empty;
    public string  LabelUnidadeVenda { get; set; } = string.Empty;
    public decimal? WholesalePrice       { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
}

/// <summary>Detalhe completo — usado pra carregar de volta no carrinho.</summary>
public class VendaSuspensaDetalheDto
{
    public Guid    Id          { get; set; }
    public Guid?   ClienteId   { get; set; }
    public string  ClienteNome { get; set; } = string.Empty;
    public List<VendaSuspensaItemDto> Itens { get; set; } = new();
}

public class SuspenderVendaDto
{
    public Guid?   ClienteId   { get; set; }
    public string  ClienteNome { get; set; } = "Sem cliente";
    public Guid    UsuarioId   { get; set; }
    public string  NomeUsuario { get; set; } = string.Empty;
    public List<VendaSuspensaItemDto> Itens { get; set; } = new();
}
