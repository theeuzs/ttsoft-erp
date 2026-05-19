using System;

namespace ERP.Domain.Entities;

public class NfePendente
{
    public Guid Id { get; set; }
    public Guid VendaId { get; set; }
    public string TipoNota { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public DateTime DataFalha { get; set; }
    public int Tentativas { get; set; }
    public string? UltimaMensagemErro { get; set; }
}