using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

// ── CRM / Follow-Up de Orçamentos ────────────────────────────────────────────

public class OrcamentoFollowUpDto
{
    public Guid            Id                 { get; set; }
    public string          Numero             { get; set; } = "";
    public string?         ClienteNome        { get; set; }
    public string?         VendedorNome       { get; set; }
    public decimal         ValorTotal         { get; set; }
    public DateTime        DataEmissao        { get; set; }
    public DateTime        DataValidade       { get; set; }
    public string          Status             { get; set; } = "";
    public DateTime?       DataFollowUp       { get; set; }
    public StatusFollowUp  StatusFollowUp     { get; set; }
    public string?         MotivoPerda        { get; set; }
    public string?         ObservacaoFollowUp { get; set; }
    public DateTime?       DataUltimoContato  { get; set; }

    /// <summary>True quando o follow-up está vencido (data passou e ainda Pendente).</summary>
    public bool FollowUpVencido =>
        DataFollowUp.HasValue &&
        DataFollowUp.Value.Date < DateTime.Today &&
        StatusFollowUp == StatusFollowUp.Pendente;
}

public class AgendarFollowUpDto
{
    public DateTime DataFollowUp { get; set; }
    public string?  Observacao   { get; set; }
}

public class RegistrarContatoDto
{
    public StatusFollowUp StatusFollowUp     { get; set; }
    public string?        ObservacaoFollowUp { get; set; }
    public string?        MotivoPerda        { get; set; }
    /// <summary>Se true, agenda novo follow-up após N dias.</summary>
    public int?           ProximoFollowUpEmDias { get; set; }
}

public class TaxaConversaoDto
{
    public int     TotalOrcamentos      { get; set; }
    public int     Convertidos          { get; set; }
    public int     Perdidos             { get; set; }
    public int     Pendentes            { get; set; }
    public decimal TaxaConversaoPercent { get; set; }
    public decimal ValorConvertido      { get; set; }
    public decimal ValorPerdido         { get; set; }
    public List<MotivosPerdaDto> TopMotivosPerdas { get; set; } = new();
}

public class MotivosPerdaDto
{
    public string Motivo    { get; set; } = "";
    public int    Quantidade { get; set; }
}

// ── Sugestão Automática de Compras ───────────────────────────────────────────

public class SugestaoCompraDto
{
    public Guid    ProductId         { get; set; }
    public string  Nome              { get; set; } = "";
    public string? SKU               { get; set; }
    public string? FornecedorNome    { get; set; }
    public Guid?   SupplierId        { get; set; }
    public decimal EstoqueAtual      { get; set; }
    public decimal EstoqueMinimo     { get; set; }
    public decimal MediaVendas30Dias { get; set; }  // unidades/dia
    public int     DiasParaRuptura   { get; set; }
    public decimal QuantidadeSugerida { get; set; }
    public decimal CustoUnitario     { get; set; }
    public decimal CustoTotalSugerido => QuantidadeSugerida * CustoUnitario;

    /// <summary>Urgência para colorir a UI: Critico (<3d), Alerta (<7d), Normal.</summary>
    public string Urgencia => DiasParaRuptura switch
    {
        <= 3  => "Critico",
        <= 7  => "Alerta",
        <= 30 => "Normal",
        _     => "Ok"
    };
}

public class GerarPedidoCompraDto
{
    public List<ItemPedidoSugeridoDto> Itens        { get; set; } = new();
    public string?                     Observacoes  { get; set; }
    public string?                     CriadoPor    { get; set; }
}

public class ItemPedidoSugeridoDto
{
    public Guid    ProductId    { get; set; }
    public string  ProductName  { get; set; } = "";
    public Guid?   SupplierId   { get; set; }
    public decimal Quantidade   { get; set; }
    public decimal PrecoUnitario { get; set; }
}
