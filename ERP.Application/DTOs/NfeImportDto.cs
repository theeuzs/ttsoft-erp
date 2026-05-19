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

public class NfeItemImportDto
{
    public string CodigoBarrasFornecedor { get; set; } = string.Empty; 
    public string NomeProdutoFornecedor { get; set; } = string.Empty;  
    public string UnidadeMedida { get; set; } = string.Empty;          
    public decimal QuantidadeComprada { get; set; }                    
    public decimal ValorCustoUnitario { get; set; }                    
    
    public Guid? ProdutoIdNoNossoSistema { get; set; }
    public bool ProdutoJaCadastrado { get; set; }
}