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

/// <summary>Uma forma de pagamento — várias podem compor o total (ex: parte
/// PIX, parte cartão). Lista vazia é válida (remessa/devolução/brinde sem
/// cobrança real) — nesse caso a emissão cai no "90 sem pagamento" sozinha.</summary>
public class NotaFiscalAvulsaPagamentoDto
{
    /// <summary>Código Focus: "01" Dinheiro, "03" Cartão Crédito, "04" Cartão
    /// Débito, "15" Boleto, "17" PIX, "90" Sem pagamento, etc.</summary>
    public string FormaPagamento { get; set; } = "90";
    public decimal Valor { get; set; }
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

    // ── Achados da revisão de arquitetura (18/08) ──────────────────────────

    /// <summary>Chave de 44 dígitos da nota original — obrigatória quando
    /// Finalidade="4" (devolução), senão a SEFAZ rejeita sumariamente.</summary>
    public string? RefNfeReferenciada { get; set; }

    public string? InformacoesComplementares { get; set; }

    /// <summary>"0" a "4" com transportadora, "9" sem frete (default).</summary>
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

    public List<NotaFiscalAvulsaItemDto> Itens { get; set; } = new();

    /// <summary>Vazio = remessa/devolução/brinde sem cobrança real (cai em
    /// "90 sem pagamento"). Preenchido = venda B2B com a forma real usada.</summary>
    public List<NotaFiscalAvulsaPagamentoDto> Pagamentos { get; set; } = new();
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

/// <summary>Tela "NF-e (Emissão)" — histórico amplo, qualquer NF-e A4 (não
/// só as avulsas, também as ligadas a uma venda, se algum dia existirem).
/// `EhAvulsa` distingue a origem só pra exibição — as ações (cancelar,
/// copiar) funcionam igual pras duas.</summary>
public record NfeA4HistoricoDto(
    Guid Id, string NaturezaOperacao, string DestinatarioNome,
    decimal ValorTotal, string Status, DateTime DataEmissao,
    bool EhAvulsa, string? UrlDanfe);

// ── Conferência (item 4/9 — "pré-visualizar" honesto: mostra os impostos
// calculados pelo MotorFiscal antes de transmitir, sem fingir renderizar
// um DANFE que só a SEFAZ pode gerar de verdade) ──────────────────────────
public record ConferenciaItemDto(
    string ProductName, decimal Quantidade, decimal ValorUnitario, decimal ValorTotal,
    ResultadoTributarioDto Tributos);

public record ConferenciaFiscalDto(
    List<ConferenciaItemDto> Itens, decimal ValorTotalProdutos, decimal ValorTotalImpostos);