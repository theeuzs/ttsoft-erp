using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public enum StatusNfse { Pendente, Autorizada, Cancelada, Erro }

/// <summary>Nota Fiscal de Serviços Eletrônica emitida via FocusNFe.</summary>
public class NfseEmitida : BaseEntity
{
    public string?     NumeroNfse      { get; set; }
    public string      ReferenciaNfse  { get; set; } = string.Empty; // Ref. interna FocusNFe
    public DateTime    DataEmissao     { get; set; } = DateTime.Now;
    public StatusNfse  Status          { get; set; } = StatusNfse.Pendente;

    // Tomador (cliente)
    public Guid?   ClienteId     { get; set; }
    public string  TomadorNome   { get; set; } = string.Empty;
    public string? TomadorCpfCnpj { get; set; }
    public string? TomadorEmail  { get; set; }

    // Serviço
    public string  DescricaoServico { get; set; } = string.Empty;
    public string? CodigoServico    { get; set; } // Código municipal
    public string? CodigoCnae      { get; set; }
    public decimal ValorServico     { get; set; }
    public decimal AliquotaISS      { get; set; } = 2m; // % ISS
    public decimal ValorISS         => Math.Round(ValorServico * AliquotaISS / 100, 2);
    public decimal ValorLiquido     => ValorServico - ValorISS;

    // Resposta FocusNFe
    public string? UrlDanfse    { get; set; }
    public string? CodigoVerificacao { get; set; }
    public string? MensagemErro  { get; set; }
    public string? JsonResposta  { get; set; }

    // Vínculo opcional com venda
    public Guid? VendaId { get; set; }
}
