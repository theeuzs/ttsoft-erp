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

    // S24 (17/08) — valor do frete no nível do documento (não por item). A
    // Focus soma isso automaticamente no valor_total da nota e monta a tag
    // vFrete certa no XML — não precisa ratear item por item manualmente.
    [JsonPropertyName("valor_frete")]
    public string? ValorFrete { get; set; }

    // Achados da revisão de arquitetura da Nota Avulsa (18/08) — nomes de
    // campo confirmados na doc oficial (campos.focusnfe.com.br/nfe/NotaFiscalXML.html).
    [JsonPropertyName("cnpj_transportador")]
    public string? CnpjTransportador { get; set; }

    [JsonPropertyName("cpf_transportador")]
    public string? CpfTransportador { get; set; }

    [JsonPropertyName("nome_transportador")]
    public string? NomeTransportador { get; set; }

    [JsonPropertyName("inscricao_estadual_transportador")]
    public string? InscricaoEstadualTransportador { get; set; }

    [JsonPropertyName("endereco_transportador")]
    public string? EnderecoTransportador { get; set; }

    [JsonPropertyName("municipio_transportador")]
    public string? MunicipioTransportador { get; set; }

    [JsonPropertyName("uf_transportador")]
    public string? UfTransportador { get; set; }

    [JsonPropertyName("veiculo_placa")]
    public string? VeiculoPlaca { get; set; }

    [JsonPropertyName("veiculo_uf")]
    public string? VeiculoUf { get; set; }

    [JsonPropertyName("volumes")]
    public List<FocusVolumeRequest>? Volumes { get; set; } = new();

    [JsonPropertyName("informacoes_adicionais_contribuinte")]
    public string? InformacoesAdicionaisContribuinte { get; set; }

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

    // Confirmado na doc oficial da Focus — sem isso, NF-e B2B toma rejeição
    // clássica dependendo do destinatário (contribuinte/isento/não contribuinte).
    [JsonPropertyName("indicador_inscricao_estadual_destinatario")]
    public string? IndicadorIeDestinatario { get; set; }

    [JsonPropertyName("itens")]
    public List<FocusItemRequest> Itens { get; set; } = new();

    [JsonPropertyName("pagamentos")]
    public List<FocusPagamentoRequest> Pagamentos { get; set; } = new();

    // Item 7 do roadmap fiscal — nota de devolução. Confirmado na doc da
    // Focus: precisa ser array (não objeto), mesmo com só uma nota referenciada.
    [JsonPropertyName("notas_referenciadas")]
    public List<NotaReferenciadaRequest>? NotasReferenciadas { get; set; } = new();
}

public class NotaReferenciadaRequest
{
    [JsonPropertyName("chave_nfe")]
    public string ChaveNfe { get; set; } = string.Empty;
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

    // Achado de auditoria (06/08/2026): ICMSSTCalculator existia mas não
    // alimentava a emissão de verdade — esses campos não existiam nem no DTO.
    // Nomes confirmados direto na doc oficial da Focus, tabela de campos por
    // CSOSN 201/202/203 (com cobrança de ICMS-ST no Simples Nacional).
    [JsonPropertyName("icms_modalidade_base_calculo_st")]
    public string? IcmsModalidadeBaseCalculoSt { get; set; } // modBCST — "4" = Margem Valor Agregado (MVA)

    [JsonPropertyName("icms_margem_valor_adicionado_st")]
    public string? IcmsMargemValorAdicionadoSt { get; set; } // pMVAST

    [JsonPropertyName("icms_base_calculo_st")]
    public string? IcmsBaseCalculoSt { get; set; } // vBCST

    [JsonPropertyName("icms_aliquota_st")]
    public string? IcmsAliquotaSt { get; set; } // pICMSST

    [JsonPropertyName("icms_valor_st")]
    public string? IcmsValorSt { get; set; } // vICMSST

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

// Volume transportado — campos confirmados na doc oficial da Focus
// (campos.focusnfe.com.br/nfe/VolumeTransportadoXML.html).
public class FocusVolumeRequest
{
    [JsonPropertyName("quantidade")]
    public string? Quantidade { get; set; }

    [JsonPropertyName("especie")]
    public string? Especie { get; set; }

    [JsonPropertyName("peso_liquido")]
    public string? PesoLiquido { get; set; }

    [JsonPropertyName("peso_bruto")]
    public string? PesoBruto { get; set; }
}