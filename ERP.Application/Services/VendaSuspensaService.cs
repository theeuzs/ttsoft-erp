// ── ERP.Application/Services/VendaSuspensaService.cs ──────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class VendaSuspensaService : IVendaSuspensaService
{
    private readonly IUnitOfWork _uow;
    public VendaSuspensaService(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> SuspenderAsync(SuspenderVendaDto dto)
    {
        var venda = new VendaSuspensa
        {
            ClienteId          = dto.ClienteId,
            ClienteNome        = string.IsNullOrWhiteSpace(dto.ClienteNome) ? "Sem cliente" : dto.ClienteNome,
            UsuarioIdSuspensor = dto.UsuarioId,
            NomeSuspensor      = dto.NomeUsuario,
            TotalAproximado    = dto.Itens.Sum(i => i.Quantity * i.NormalUnitPrice),
            Status             = StatusVendaSuspensa.Suspensa,
            Itens              = dto.Itens.Select(MapParaEntidade).ToList()
        };

        await _uow.VendasSuspensas.AddAsync(venda);
        await _uow.CommitAsync();
        return venda.Id;
    }

    public async Task<IReadOnlyList<VendaSuspensaResumoDto>> ObterPendentesAsync()
    {
        var pendentes = await _uow.VendasSuspensas.GetPendentesAsync();
        var lista = pendentes.ToList();

        return lista.Select(v => new VendaSuspensaResumoDto
        {
            Id               = v.Id,
            DataSuspensao    = v.DataSuspensao,
            ClienteNome      = v.ClienteNome,
            QuantidadeItens  = v.Itens.Count,
            TotalAproximado  = v.TotalAproximado,
            NomeSuspensor    = v.NomeSuspensor,
            EmEdicao         = v.UsuarioIdEmEdicao.HasValue,
            NomeEmEdicao     = v.NomeEmEdicao,
            DataInicioEdicao = v.DataInicioEdicao
        }).ToList();
    }

    public async Task<VendaSuspensaDetalheDto> IniciarEdicaoAsync(Guid id, Guid usuarioId, string nomeUsuario)
    {
        var venda = await _uow.VendasSuspensas.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Venda suspensa não encontrada — pode já ter sido retomada por outro operador.");

        if (venda.Status != StatusVendaSuspensa.Suspensa)
            throw new InvalidOperationException("Essa venda suspensa já foi finalizada ou descartada.");

        if (venda.UsuarioIdEmEdicao.HasValue && venda.UsuarioIdEmEdicao.Value != usuarioId)
            throw new InvalidOperationException(
                $"Está sendo editada por {venda.NomeEmEdicao} desde {venda.DataInicioEdicao:HH:mm}.");

        // S17 FIX: leitura já veio sem tracking (GetByIdAsync) — update é direto
        // via ExecuteUpdateAsync, sem prender a entidade no change tracker. Evita
        // o erro "another instance with the same key is already being tracked"
        // quando duas ações tocam o mesmo registro quase ao mesmo tempo (ex:
        // clique duplo disparando o comando duas vezes).
        await _uow.VendasSuspensas.IniciarEdicaoAsync(id, usuarioId, nomeUsuario, DateTime.Now);

        return new VendaSuspensaDetalheDto
        {
            Id          = venda.Id,
            ClienteId   = venda.ClienteId,
            ClienteNome = venda.ClienteNome,
            Itens       = venda.Itens.Select(MapParaDto).ToList()
        };
    }

    public async Task LiberarEdicaoAsync(Guid id)
        => await _uow.VendasSuspensas.LiberarEdicaoAsync(id);

    public async Task AtualizarESuspenderAsync(Guid id, SuspenderVendaDto dto)
    {
        await _uow.VendasSuspensas.RemoverItensAsync(id);

        var novosItens = dto.Itens.Select(i =>
        {
            var item = MapParaEntidade(i);
            item.VendaSuspensaId = id;
            return item;
        });
        await _uow.VendasSuspensas.AdicionarItensAsync(novosItens);
        await _uow.CommitAsync();

        var totalAproximado = dto.Itens.Sum(i => i.Quantity * i.NormalUnitPrice);
        var clienteNome = string.IsNullOrWhiteSpace(dto.ClienteNome) ? "Sem cliente" : dto.ClienteNome;
        await _uow.VendasSuspensas.AtualizarCabecalhoESuspenderAsync(id, dto.ClienteId, clienteNome, totalAproximado);
    }

    public async Task FinalizarAsync(Guid id, Guid vendaId)
        => await _uow.VendasSuspensas.FinalizarAsync(id, vendaId);

    public async Task DescartarAsync(Guid id)
        => await _uow.VendasSuspensas.DescartarAsync(id);

    private static VendaSuspensaItem MapParaEntidade(VendaSuspensaItemDto i) => new()
    {
        ProductId         = i.ProductId,
        ProductName       = i.ProductName ?? string.Empty,
        Quantity          = i.Quantity,
        NormalUnitPrice   = i.NormalUnitPrice,
        UnitPrice         = i.UnitPrice,
        Observacao        = i.Observacao ?? string.Empty,
        FatorConversao    = i.FatorConversao,
        UnidadeEstoque    = i.UnidadeEstoque ?? string.Empty,
        LabelUnidadeVenda = i.LabelUnidadeVenda ?? string.Empty,
        WholesalePrice       = i.WholesalePrice,
        WholesaleMinQuantity = i.WholesaleMinQuantity
    };

    private static VendaSuspensaItemDto MapParaDto(VendaSuspensaItem i) => new()
    {
        ProductId         = i.ProductId,
        ProductName       = i.ProductName,
        Quantity          = i.Quantity,
        NormalUnitPrice   = i.NormalUnitPrice,
        UnitPrice         = i.UnitPrice,
        Observacao        = i.Observacao,
        FatorConversao    = i.FatorConversao,
        UnidadeEstoque    = i.UnidadeEstoque,
        LabelUnidadeVenda = i.LabelUnidadeVenda,
        WholesalePrice       = i.WholesalePrice,
        WholesaleMinQuantity = i.WholesaleMinQuantity
    };
}