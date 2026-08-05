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
    public DateTime DataEmissao { get; set; } = DateTime.Now;
    public string? MotivoCancelamento { get; set; }

    /// <summary>Nome do destinatário — preenchido tanto pra nota de venda
    /// (snapshot do Customer) quanto pra nota avulsa (não tem Customer
    /// cadastrado necessariamente).</summary>
    public string? DestinatarioNome { get; set; }
    public string? DestinatarioDocumento { get; set; }
}
