// ── ERP.Domain/Interfaces/IVendaSuspensaRepository.cs ─────────────────────────
using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface IVendaSuspensaRepository
{
    Task AddAsync(VendaSuspensa venda);

    /// <summary>Sempre sem tracking — leitura pura, nunca usada pra depois chamar Update() em cima.</summary>
    Task<VendaSuspensa?> GetByIdAsync(Guid id);

    Task<IEnumerable<VendaSuspensa>> GetPendentesAsync();

    Task RemoverItensAsync(Guid vendaSuspensaId);
    Task AdicionarItensAsync(IEnumerable<VendaSuspensaItem> itens);

    // ── Updates direcionados via ExecuteUpdateAsync — nunca via tracking, pra
    // evitar "another instance with the same key is already being tracked"
    // quando duas chamadas na mesma janela de diálogo tocam o mesmo registro
    // (ex: duplo clique disparando duas leituras quase simultâneas). ──────────

    Task IniciarEdicaoAsync(Guid id, Guid usuarioId, string nomeUsuario, DateTime dataInicio);
    Task LiberarEdicaoAsync(Guid id);
    Task AtualizarCabecalhoESuspenderAsync(Guid id, Guid? clienteId, string clienteNome, decimal totalAproximado);
    Task FinalizarAsync(Guid id, Guid vendaId);
    Task DescartarAsync(Guid id);
}