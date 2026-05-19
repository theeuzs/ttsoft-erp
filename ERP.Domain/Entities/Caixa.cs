using ERP.Domain.Common;
using ERP.Domain.Enums;
using System;
using System.Collections.Generic;

namespace ERP.Domain.Entities;

public class Caixa : BaseEntity
{
    // O número sequencial para ficar fácil de ler (Ex: Caixa #1, Caixa #2)
    public int NumeroCaixa { get; set; } 
    
    public Guid UsuarioId { get; set; } // ID de quem abriu o caixa
    public string OperadorNome { get; set; } = string.Empty; // Nome de quem abriu
    
    public DateTime DataAbertura { get; set; } = DateTime.Now;
    public DateTime? DataFechamento { get; set; }
    public decimal ValorAbertura { get; set; }
    public StatusCaixa Status { get; set; } = StatusCaixa.Aberto;

    // Relacionamento (1 Caixa tem Vários Movimentos)
    public List<CaixaMovimento> Movimentos { get; set; } = new();
}