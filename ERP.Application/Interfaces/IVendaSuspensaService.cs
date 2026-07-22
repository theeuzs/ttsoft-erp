// ── ERP.Application/Interfaces/IVendaSuspensaService.cs ───────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IVendaSuspensaService
{
    Task<Guid> SuspenderAsync(SuspenderVendaDto dto);

    Task<IReadOnlyList<VendaSuspensaResumoDto>> ObterPendentesAsync();

    /// <summary>
    /// Abre pra retomar — trava a edição pro usuário. Lança exceção se já
    /// estiver travada por OUTRO usuário (mesmo usuário reabrindo é permitido).
    /// </summary>
    Task<VendaSuspensaDetalheDto> IniciarEdicaoAsync(Guid id, Guid usuarioId, string nomeUsuario);

    /// <summary>Libera a trava sem mudar o status — usado quando o operador desiste sem finalizar nem descartar.</summary>
    Task LiberarEdicaoAsync(Guid id);

    /// <summary>Re-suspende a MESMA venda suspensa (não cria uma nova) — atualiza itens e libera a trava.</summary>
    Task AtualizarESuspenderAsync(Guid id, SuspenderVendaDto dto);

    Task FinalizarAsync(Guid id, Guid vendaId);
    Task DescartarAsync(Guid id);
}
