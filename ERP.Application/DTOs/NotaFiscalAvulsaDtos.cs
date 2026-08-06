// ── ERP.Application/DTOs/NotaFiscalAvulsaDtos.cs ────────────────────────────
namespace ERP.Application.DTOs;

public class NotaFiscalAvulsaItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public string Cfop { get; set; } = "5102";
}

/// <summary>DTO de entrada — salvar (criar/atualizar) um rascunho.</summary>
public class SalvarNotaFiscalAvulsaDto
{
    /// <summary>Nulo = criar novo rascunho.</summary>
    public Guid? Id { get; set; }

    public string NaturezaOperacao { get; set; } = "VENDA DE MERCADORIA";
    /// <summary>"E" ou "S".</summary>
    public string TipoOperacaoEntradaSaida { get; set; } = "S";
    /// <summary>Convenção Focus: "1"=normal, "4"=devolução — antes nunca era
    /// persistida do rascunho, sempre ficava "1" mesmo escolhendo devolução.</summary>
    public string Finalidade { get; set; } = "1";

    public string DestinatarioNome { get; set; } = string.Empty;
    public string? DestinatarioDocumento { get; set; }
    public string? DestinatarioLogradouro { get; set; }
    public string? DestinatarioNumero { get; set; }
    public string? DestinatarioBairro { get; set; }
    public string? DestinatarioMunicipio { get; set; }
    public string? DestinatarioUf { get; set; }
    public string? DestinatarioCep { get; set; }
    public string? DestinatarioIe { get; set; }

    /// <summary>"1"=contribuinte ICMS, "2"=isento, "9"=não contribuinte.</summary>
    public string IndicadorIeDestinatario { get; set; } = "9";

    public List<NotaFiscalAvulsaItemDto> Itens { get; set; } = new();
}

/// <summary>DTO de leitura — carregar um rascunho pra editar.</summary>
public class NotaFiscalAvulsaDto : SalvarNotaFiscalAvulsaDto
{
    public new Guid Id { get; set; }
    public string Status { get; set; } = "Rascunho";
    public string? UrlDanfe { get; set; }
    public DateTime DataEmissao { get; set; }
}

/// <summary>DTO enxuto pra listagem de rascunhos/notas avulsas.</summary>
public record NotaFiscalAvulsaResumoDto(
    Guid Id, string NaturezaOperacao, string DestinatarioNome,
    decimal ValorTotal, string Status, DateTime DataEmissao);

// ── Conferência (item 4/9 — "pré-visualizar" honesto: mostra os impostos
// calculados pelo MotorFiscal antes de transmitir, sem fingir renderizar
// um DANFE que só a SEFAZ pode gerar de verdade) ──────────────────────────
public record ConferenciaItemDto(
    string ProductName, decimal Quantidade, decimal ValorUnitario, decimal ValorTotal,
    ResultadoTributarioDto Tributos);

public record ConferenciaFiscalDto(
    List<ConferenciaItemDto> Itens, decimal ValorTotalProdutos, decimal ValorTotalImpostos);