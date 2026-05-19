using ERP.Domain.Common;
using ERP.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Domain.Entities;

public class Orcamento : BaseEntity   // ← era: public class Orcamento (sem herança)
{
    // Id, TenantId, CreatedAt, UpdatedAt e IsDeleted vêm do BaseEntity
    // Removidos os campos manuais: public Guid Id e public Guid TenantId

    public string Numero { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? SellerName { get; set; }
    public Guid UsuarioId { get; set; }

    public DateTime DataEmissao { get; set; }
    public DateTime DataValidade { get; set; }
    public decimal ValorTotal { get; set; }

    public StatusOrcamento Status { get; set; } = StatusOrcamento.Aberto;

    // ── CRM / Follow-Up ───────────────────────────────────────────────────────
    /// <summary>Data agendada para o vendedor contatar o cliente sobre este orçamento.</summary>
    public DateTime? DataFollowUp { get; set; }

    /// <summary>Status do follow-up: Pendente → Contatado → Convertido/Perdido.</summary>
    public StatusFollowUp StatusFollowUp { get; set; } = StatusFollowUp.Pendente;

    /// <summary>Motivo da perda (preço, concorrente, sem interesse, etc.).</summary>
    public string? MotivoPerda { get; set; }

    /// <summary>Observações do contato realizado.</summary>
    public string? ObservacaoFollowUp { get; set; }

    /// <summary>Data e hora do último contato registrado.</summary>
    public DateTime? DataUltimoContato { get; set; }

    public ICollection<OrcamentoItem> Itens { get; set; } = new List<OrcamentoItem>();

    public void RecalcularTotal()
    {
        ValorTotal = Itens.Sum(i => i.Total);
    }

    public Orcamento()
    {
        DataEmissao  = DateTime.Now;
        DataValidade = DateTime.Now.AddDays(7);
    }
}