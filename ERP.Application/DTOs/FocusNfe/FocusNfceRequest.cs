using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ERP.Application.DTOs.FocusNfe;

// Essa classe representa a Nota Fiscal inteira
public class FocusNfceRequest
{
    [JsonPropertyName("cnpj_emitente")]
    public string CnpjEmitente { get; set; } = "12820608000141";

    [JsonPropertyName("natureza_operacao")]
    public string NaturezaOperacao { get; set; } = "VENDA DE MERCADORIA";

    [JsonPropertyName("data_emissao")]
    public string DataEmissao { get; set; } = string.Empty;

    [JsonPropertyName("tipo_documento")]
    public string TipoDocumento { get; set; } = "2"; // 1 = NFe (A4), 2 = NFCe (Cupom)

    [JsonPropertyName("presenca_comprador")]
    public string PresencaComprador { get; set; } = "1"; // 1 = Operação presencial

    [JsonPropertyName("consumidor_final")]
    public string ConsumidorFinal { get; set; } = "1"; // 1 = Sim

    [JsonPropertyName("finalidade_emissao")]
    public string FinalidadeEmissao { get; set; } = "1"; // 1 = Normal

    [JsonPropertyName("modalidade_frete")]
    public string ModalidadeFrete { get; set; } = "9";

    [JsonPropertyName("nome_destinatario")]
    public string? Nome { get; set; }

    [JsonIgnore] // Esconde essa variável da Focus para não dar erro
    public string? CpfCnpj { get; set; }

    // Se tiver até 11 números, o C# manda como CPF automaticamente!
    [JsonPropertyName("cpf_destinatario")]
    public string? CpfDestinatario => CpfCnpj?.Length <= 11 ? CpfCnpj : null;

    // Se tiver mais de 11 números, o C# manda como CNPJ automaticamente!
    [JsonPropertyName("cnpj_destinatario")]
    public string? CnpjDestinatario => CpfCnpj?.Length > 11 ? CpfCnpj : null;

    [JsonPropertyName("logradouro_destinatario")]
    public string? LogradouroDestinatario { get; set; }

    [JsonPropertyName("numero_destinatario")]
    public string? NumeroDestinatario { get; set; }

    [JsonPropertyName("bairro_destinatario")]
    public string? BairroDestinatario { get; set; }

    [JsonPropertyName("municipio_destinatario")]
    public string? MunicipioDestinatario { get; set; }

    [JsonPropertyName("uf_destinatario")]
    public string? UfDestinatario { get; set; }

    [JsonPropertyName("cep_destinatario")]
    public string? CepDestinatario { get; set; }

    [JsonPropertyName("inscricao_estadual_destinatario")]
    public string? IeDestinatario { get; set; }

    [JsonPropertyName("itens")]
    public List<FocusItemRequest> Itens { get; set; } = new();

    [JsonPropertyName("pagamentos")]
    public List<FocusPagamentoRequest> Pagamentos { get; set; } = new();
}

// Essa classe representa cada produto do cupom
public class FocusItemRequest
{
    [JsonPropertyName("numero_item")]
    public string NumeroItem { get; set; } = string.Empty;

    [JsonPropertyName("codigo_produto")]
    public string CodigoProduto { get; set; } = string.Empty;

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [JsonPropertyName("cfop")]
    public string Cfop { get; set; } = "5102"; // Venda de mercadoria

    [JsonPropertyName("unidade_comercial")]
    public string UnidadeComercial { get; set; } = "UN";

    [JsonPropertyName("quantidade_comercial")]
    public string QuantidadeComercial { get; set; } = string.Empty;

    [JsonPropertyName("valor_unitario_comercial")]
    public string ValorUnitarioComercial { get; set; } = string.Empty;

    [JsonPropertyName("valor_bruto")]
    public string ValorBruto { get; set; } = string.Empty;

    // 👇 A TRINDADE SAGRADA QUE EU TINHA ESQUECIDO 👇

    [JsonPropertyName("codigo_ncm")]
    public string CodigoNcm { get; set; } = "25232910"; // NCM Chumbado do Cimento (já vamos deixar isso dinâmico depois)

    [JsonPropertyName("icms_origem")]
    public string IcmsOrigem { get; set; } = "0"; // 0 = Nacional, 1 = Importado

    [JsonPropertyName("icms_situacao_tributaria")]
    public string IcmsSituacaoTributaria { get; set; } = "102"; // O seu CSOSN (Tributada pelo Simples Nacional sem permissão de crédito)

    [JsonPropertyName("pis_situacao_tributaria")]
    public string PisSituacaoTributaria { get; set; } = "99";

    [JsonPropertyName("cofins_situacao_tributaria")]
    public string CofinsSituacaoTributaria { get; set; } = "99";
}

// Essa classe representa como o cliente pagou (Dinheiro, PIX, Cartão)
public class FocusPagamentoRequest
{
    [JsonPropertyName("forma_pagamento")]
    public string FormaPagamento { get; set; } = string.Empty; // 01=Dinheiro, 03=Cartão Crédito, 04=Cartão Débito, 17=PIX

    [JsonPropertyName("valor_pagamento")]
    public string ValorPagamento { get; set; } = string.Empty;
}