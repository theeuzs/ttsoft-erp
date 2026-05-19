using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IEntregaService
{
    // ── CRUD ─────────────────────────────────────────────────────────────────
    Task<IEnumerable<EntregaDto>> GetAllAsync(DateTime? data = null, string? status = null, Guid? motoristaId = null);
    Task<EntregaDto?>             GetByIdAsync(Guid id);
    Task<EntregaDto?>             GetBySaleAsync(Guid saleId);
    Task<EntregaDto>              CreateAsync(CreateEntregaDto dto);
    Task                          DeleteAsync(Guid id);

    // ── Operação ──────────────────────────────────────────────────────────────
    Task<EntregaDto> AtualizarStatusAsync(Guid id, AtualizarStatusEntregaDto dto);
    Task<EntregaDto> AtribuirMotoristaAsync(Guid id, AtribuirMotoristaDto dto);

    // ── Relatórios ────────────────────────────────────────────────────────────
    Task<RelatorioEntregasDto> GetRelatorioAsync(DateTime data);

    // ── Veículos ──────────────────────────────────────────────────────────────
    Task<IEnumerable<VeiculoDto>> GetVeiculosAsync();
    Task<VeiculoDto>              CreateVeiculoAsync(CreateVeiculoDto dto);
    Task                          DeleteVeiculoAsync(Guid id);
}
