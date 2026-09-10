// ── ERP.Domain/Entities/NotaFiscal.cs ───────────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Fundação do módulo fiscal (análise de 05/08) — hoje o dado fiscal mora
/// como colunas da própria Sale (NfceChave, NfceNumero, etc.), o que funciona
/// pra nota-de-venda mas impede nota avulsa (sem venda nenhuma por trás),
/// NFS-e, e uma grid de Notas Fiscais de verdade com filtro/busca. Essa
/// entidade não substitui as colunas da Sale ainda (mantidas por
/// compatibilidade) — FiscalService passa a gravar dos dois lados, e telas
/// novas (nota avulsa, NFS-e, emitir pelo histórico) usam só essa tabela.
/// </summary>
public class NotaFiscal : BaseEntity
{
    /// <summary>"NFCE", "NFE", "NFSE".</summary>
    public string Tipo { get; set; } = string.Empty;

    public string? Chave { get; set; }
    public string? Numero { get; set; }
    public string? Serie { get; set; }

    /// <summary>"Pendente", "Autorizada", "Cancelada", "Rejeitada", "Contingência".</summary>
    public string Status { get; set; } = "Pendente";

    /// <summary>Convenção Focus: "1"=normal, "4"=devolução de mercadoria.</summary>
    public string Finalidade { get; set; } = "1";

    /// <summary>Nulo = nota avulsa, sem venda por trás.</summary>
    public Guid? VendaId { get; set; }
    public Sale? Venda { get; set; }

    /// <summary>Chave da nota referenciada — usado em devolução e CC-e.</summary>
    public string? RefNFe { get; set; }

    public string? UrlDanfe { get; set; }
    public string? XmlUrl { get; set; }
    public string Ambiente { get; set; } = string.Empty;
    public DateTime DataEmissao { get; set; } = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
    public string? MotivoCancelamento { get; set; }

    /// <summary>Nome do destinatário — preenchido tanto pra nota de venda
    /// (snapshot do Customer) quanto pra nota avulsa (não tem Customer
    /// cadastrado necessariamente).</summary>
    public string? DestinatarioNome { get; set; }
    public string? DestinatarioDocumento { get; set; }

    // ── Item 9 do roadmap fiscal: nota avulsa ──────────────────────────────
    /// <summary>"Rascunho" (mais um valor de Status) — nota salva mas ainda
    /// não enviada à SEFAZ. Só rascunho pode ser editado/excluído.</summary>
    public string? NaturezaOperacao { get; set; }

    /// <summary>"E" (entrada) ou "S" (saída) — nota avulsa pode ser
    /// remessa, transferência, etc., não só venda.</summary>
    public string TipoOperacaoEntradaSaida { get; set; } = "S";

    public string? DestinatarioLogradouro { get; set; }
    public string? DestinatarioNumero { get; set; }
    public string? DestinatarioBairro { get; set; }
    public string? DestinatarioMunicipio { get; set; }
    public string? DestinatarioUf { get; set; }
    public string? DestinatarioCep { get; set; }
    public string? DestinatarioIe { get; set; }

    /// <summary>"1"=contribuinte ICMS (tem IE), "2"=isento, "9"=não
    /// contribuinte — sem mandar isso pra Focus, NF-e B2B toma rejeição
    /// clássica dependendo do destinatário.</summary>
    public string IndicadorIeDestinatario { get; set; } = "9";

    // ── Achados da revisão de arquitetura (18/08) — a tela de Nota Avulsa
    // travava origem/pagamento/transporte, impossibilitando devolução (SEFAZ
    // exige a nota referenciada) e venda B2B real (forma de pagamento fixa
    // em "sem pagamento"). Confirmado com o dono do sistema que os dois
    // cenários acontecem de verdade — não é hipotético.

    /// <summary>Texto livre — vira infCpl na Focus. Quase toda nota avulsa
    /// precisa de alguma observação (motivo da devolução, dados bancários,
    /// exigência legal do regime).</summary>
    public string? InformacoesComplementares { get; set; }

    // ── Transporte — só relevante quando a mercadoria sai por transportadora,
    // não em retirada/entrega própria. Todos opcionais; ModalidadeFrete "9"
    // (sem frete) é o default seguro quando nada disso se aplica.
    public string ModalidadeFrete { get; set; } = "9";
    public string? TransportadoraNome { get; set; }
    public string? TransportadoraDocumento { get; set; }
    public string? TransportadoraIe { get; set; }
    public string? TransportadoraEndereco { get; set; }
    public string? TransportadoraMunicipio { get; set; }
    public string? TransportadoraUf { get; set; }
    public string? VeiculoPlaca { get; set; }
    public string? VeiculoUf { get; set; }
    public int? QuantidadeVolumes { get; set; }
    public string? EspecieVolumes { get; set; }
    public decimal? PesoBrutoKg { get; set; }
    public decimal? PesoLiquidoKg { get; set; }

    public ICollection<NotaFiscalItem> Itens { get; set; } = new List<NotaFiscalItem>();

    /// <summary>Formas de pagamento da nota — vazio é válido (remessa,
    /// devolução, brinde: "sem pagamento" de verdade). Preenchido: venda
    /// B2B formalizada fora do PDV, com a forma real usada.</summary>
    public ICollection<NotaFiscalPagamento> Pagamentos { get; set; } = new List<NotaFiscalPagamento>();
}

/// <summary>Uma forma de pagamento de uma nota avulsa — várias formas podem
/// compor o mesmo total (ex: parte PIX, parte cartão).</summary>
public class NotaFiscalPagamento : BaseEntity
{
    public Guid NotaFiscalId { get; set; }
    public NotaFiscal NotaFiscal { get; set; } = null!;

    /// <summary>Código Focus: "01" Dinheiro, "03" Cartão Crédito, "04" Cartão
    /// Débito, "15" Boleto, "17" PIX, "90" Sem pagamento, etc.</summary>
    public string FormaPagamento { get; set; } = "90";
    public decimal Valor { get; set; }
}

/// <summary>Item de uma nota avulsa (rascunho ou já emitida) — não existe
/// pra notas de venda (essas usam SaleItem, via VendaId).</summary>
public class NotaFiscalItem : BaseEntity
{
    public Guid NotaFiscalId { get; set; }
    public NotaFiscal NotaFiscal { get; set; } = null!;

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }

    /// <summary>Por item, sobrescreve o CFOP padrão do produto — nota avulsa
    /// pode ter operação diferente de venda normal (remessa, transferência).</summary>
    public string Cfop { get; set; } = "5102";
}