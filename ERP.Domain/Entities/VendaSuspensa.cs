// ── ERP.Domain/Entities/VendaSuspensa.cs ──────────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Carrinho suspenso no balcão (fila, cliente foi buscar mais um item) —
/// persiste no banco (S17: antes era só em memória, sumia se o PC travasse).
/// Não é Sale (não gera obrigação fiscal) nem Orçamento (que é formal, com
/// validade e follow-up) — é um conceito próprio: "pausa operacional de balcão".
/// </summary>
public class VendaSuspensa : BaseEntity
{
    public DateTime DataSuspensao { get; set; } = DateTime.Now;

    public Guid?  ClienteId   { get; set; }
    public string ClienteNome { get; set; } = "Sem cliente";

    public Guid UsuarioIdSuspensor { get; set; }
    public string NomeSuspensor    { get; set; } = string.Empty;

    public decimal TotalAproximado { get; set; }

    public StatusVendaSuspensa Status { get; set; } = StatusVendaSuspensa.Suspensa;

    /// <summary>Venda real gerada quando essa suspensa foi finalizada — rastreabilidade.</summary>
    public Guid? VendaFinalizadaId { get; set; }

    /// <summary>
    /// Trava temporária de edição — NÃO é o status. Setada quando um operador
    /// abre pra retomar; liberada (volta a null) se ele desistir sem finalizar
    /// nem descartar, ou quando finaliza/descarta de fato.
    /// </summary>
    public Guid?     UsuarioIdEmEdicao { get; set; }
    public string?    NomeEmEdicao     { get; set; }
    public DateTime?  DataInicioEdicao { get; set; }

    public List<VendaSuspensaItem> Itens { get; set; } = new();
}
