using System;
using System.Collections.Generic;

namespace ERP.Application.DTOs;

public class NfeImportDto
{
    public string NumeroNota { get; set; } = string.Empty;
    public string ChaveAcesso { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public string FornecedorCnpj { get; set; } = string.Empty;
    public DateTime DataEmissao { get; set; }
    public decimal ValorTotal { get; set; }
    /// <summary>S17: soma dos produtos (vProd do XML) — sem IPI/frete/outros. Comparável com a soma dos itens conferidos, diferente de ValorTotal (vNF), que inclui impostos.</summary>
    public decimal TotalProdutosXml { get; set; }
    
    public List<NfeItemImportDto> Itens { get; set; } = new();
    
    // 👇 NOVO: A lista que vai guardar as parcelas/boletos da nota
    public List<NfeDuplicataDto> Duplicatas { get; set; } = new();
}

// 👇 NOVO: A classe que representa uma parcela do boleto
public class NfeDuplicataDto
{
    public string Numero { get; set; } = string.Empty;
    public DateTime DataVencimento { get; set; }
    public decimal Valor { get; set; }
}

public class NfeItemImportDto : System.ComponentModel.INotifyPropertyChanged
{
    public string CodigoBarrasFornecedor { get; set; } = string.Empty; 
    public string NomeProdutoFornecedor { get; set; } = string.Empty;  
    public string UnidadeMedida { get; set; } = string.Empty;          
    public decimal QuantidadeComprada { get; set; }                    
    public decimal ValorCustoUnitario { get; set; }                    
    
    public Guid? ProdutoIdNoNossoSistema { get; set; }
    public bool ProdutoJaCadastrado { get; set; }

    // S17: conferência de recebimento — nasce igual ao XML, mas pode ser
    // ajustada se a mercadoria que chegou for diferente do documento fiscal.
    // O XML (QuantidadeComprada/ValorCustoUnitario) nunca é alterado — é o
    // documento fiscal. Estoque e Pedido de Compra passam a usar os valores
    // CONFERIDOS; Contas a Pagar continua vindo só das duplicatas/total do
    // XML, sem nenhuma relação com isso (já era assim antes).
    private decimal _quantidadeConferida;
    public decimal QuantidadeConferida
    {
        get => _quantidadeConferida;
        set { _quantidadeConferida = value; OnPropertyChanged(nameof(QuantidadeConferida)); OnPropertyChanged(nameof(DiferencaQuantidade)); OnPropertyChanged(nameof(TemDivergencia)); }
    }

    private decimal _custoConferido;
    public decimal CustoConferido
    {
        get => _custoConferido;
        set { _custoConferido = value; OnPropertyChanged(nameof(CustoConferido)); OnPropertyChanged(nameof(DiferencaCusto)); OnPropertyChanged(nameof(TemDivergencia)); }
    }

    public decimal DiferencaQuantidade => QuantidadeConferida - QuantidadeComprada;
    public decimal DiferencaCusto      => CustoConferido - ValorCustoUnitario;
    public bool    TemDivergencia      => DiferencaQuantidade != 0 || DiferencaCusto != 0;

    /// <summary>Chamar depois de montar o item a partir do XML, pra Conferido nascer igual.</summary>
    public void InicializarConferenciaComXml()
    {
        _quantidadeConferida = QuantidadeComprada;
        _custoConferido      = ValorCustoUnitario;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string nome) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nome));
}