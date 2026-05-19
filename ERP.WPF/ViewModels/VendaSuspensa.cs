using System;
using System.Collections.Generic;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Representa uma venda pausada/suspensa que pode ser retomada.
/// Armazenada em memória — não persiste no banco (design intencional
/// para não gerar registros fiscais de vendas não finalizadas).
/// </summary>
public class VendaSuspensa
{
    public Guid          Id               { get; set; } = Guid.NewGuid();
    public DateTime      HoraSuspensao    { get; set; } = DateTime.Now;
    public string        ClienteNome      { get; set; } = "Sem cliente";
    public decimal       TotalAproximado  { get; set; }
    public List<CartItem> Itens           { get; set; } = new();

    /// <summary>Exibição amigável para a lista de vendas suspensas.</summary>
    public string Resumo =>
        $"[{HoraSuspensao:HH:mm}] {ClienteNome} — {TotalAproximado:C}";
}
