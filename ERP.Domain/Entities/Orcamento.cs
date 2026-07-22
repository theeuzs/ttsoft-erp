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

    /// <summary>Observação livre do vendedor no momento de salvar o orçamento — nunca existiu antes.</summary>
    public string? Observacao { get; set; }

    // S17: StatusFollowUp = Pendente é o valor PADRÃO de todo orçamento, mesmo
    // sem follow-up nenhum agendado — essas duas propriedades existem só pra
    // a tela distinguir "tem follow-up de verdade marcado" sem depender de
    // comparação de nulo direto no XAML (fonte de bug: InverseBoolToVisibility
    // não existe no projeto, causou crash).
    public bool TemFollowUpAgendado => DataFollowUp.HasValue;
    public bool SemFollowUpAgendado => !DataFollowUp.HasValue;

    /// <summary>Combina observação + motivo de perda num texto só, pra tooltip — sem isso, os dois campos eram salvos mas nunca apareciam em tela nenhuma.</summary>
    public string TextoObservacoesFollowUp
    {
        get
        {
            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(ObservacaoFollowUp)) partes.Add($"Observação: {ObservacaoFollowUp}");
            if (!string.IsNullOrWhiteSpace(MotivoPerda)) partes.Add($"Motivo da perda: {MotivoPerda}");
            return partes.Count > 0 ? string.Join("\n", partes) : "Sem observações registradas.";
        }
    }

    public bool TemObservacaoOuMotivo =>
        !string.IsNullOrWhiteSpace(ObservacaoFollowUp) || !string.IsNullOrWhiteSpace(MotivoPerda);

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