// ── ERP.Domain/Entities/ContaReceberEvento.cs ──────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Log append-only de tudo que aconteceu com uma ContaReceber — nunca é
/// atualizado nem apagado, só inserido. Mesma filosofia do OrderEvent do
/// módulo de marketplace: em vez de só guardar o estado final (Status,
/// ValorRecebido, ValorDesconto), guarda a linha do tempo de como chegou lá —
/// resolve "quem cancelou essa conta e por quê" sem precisar confiar só no
/// motivo escrito na Descricao (que só cabe o cancelamento mais recente).
/// </summary>
public class ContaReceberEvento : BaseEntity
{
    public Guid           ContaReceberId { get; set; }
    public ContaReceber?  ContaReceber   { get; set; }

    /// <summary>"Criada", "Desconto", "Pagamento", "Cancelamento".</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Quem fez a ação — nulo em contextos sem usuário autenticado
    /// (ex: repasse automático do Mercado Livre criando a conta).</summary>
    public Guid?   UsuarioId   { get; set; }
    public string? UsuarioNome { get; set; }

    /// <summary>Valor relevante ao evento (o desconto dado, o valor pago,
    /// o total da conta na criação) — nulo pra eventos sem valor associado.</summary>
    public decimal? Valor { get; set; }

    /// <summary>Motivo, forma de pagamento, ou qualquer outro detalhe do evento.</summary>
    public string? Observacao { get; set; }

    public DateTime DataEvento { get; set; } = DateTime.UtcNow;
}
