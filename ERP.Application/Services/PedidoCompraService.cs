// ERP.Application/Services/PedidoCompraService.cs
// ─────────────────────────────────────────────────────────────────────────────
// ANTES: ERP.Infrastructure/Services/PedidoCompraService.cs (errado — lógica
//        de negócio misturada com acesso a dados na Infrastructure layer).
// AGORA: ERP.Application/Services/PedidoCompraService.cs (Clean Architecture).
//        Usa IUnitOfWork em vez de AppDbContext direto.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class PedidoCompraService : IPedidoCompraService
{
    private readonly IUnitOfWork _uow;

    public PedidoCompraService(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<PedidoCompraDto>> GetAllAsync()
    {
        var pedidos = await _uow.PedidosCompra.GetAllAsync();
        // GetAllAsync retorna a coleção base; para eager-load de Itens e Supplier
        // o repositório deve incluí-los. Se precisar de includes adicionais,
        // adicione um método GetAllWithItensAsync em IPedidoCompraRepository.
        return pedidos.Select(MapToDto);
    }

    public async Task<PedidoCompraDto?> GetByIdAsync(Guid id)
    {
        var pedido = await _uow.PedidosCompra.GetWithItensAsync(id);
        return pedido is null ? null : MapToDto(pedido);
    }

    public async Task<PedidoCompraDto> CriarAsync(CreatePedidoCompraDto dto)
    {
        // Gera número sequencial via repositório (sem acesso direto ao contexto)
        string numero = await _uow.PedidosCompra.GerarProximoNumeroAsync();

        var pedido = new PedidoCompra
        {
            Numero         = numero,
            SupplierId     = dto.SupplierId,
            FornecedorNome = dto.FornecedorNome,
            DataPrevista   = dto.DataPrevista,
            Observacoes    = dto.Observacoes,
            CriadoPor      = dto.CriadoPor,
            DataPedido     = DateTime.UtcNow,
            CreatedAt      = DateTime.UtcNow,
        };

        foreach (var item in dto.Itens)
        {
            pedido.Itens.Add(new PedidoCompraItem
            {
                ProductId     = item.ProductId,
                ProductName   = item.ProductName,
                Quantidade    = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                CreatedAt     = DateTime.UtcNow,
            });
        }

        await _uow.PedidosCompra.AddAsync(pedido);
        await _uow.CommitAsync();

        return MapToDto(pedido);
    }

    public async Task AtualizarAsync(Guid id, AtualizarPedidoCompraDto dto)
    {
        var pedido = await _uow.PedidosCompra.GetWithItensAsync(id)
            ?? throw new KeyNotFoundException($"Pedido {id} não encontrado.");

        // Editar() já valida Status == Rascunho e lança se não for.
        pedido.Editar(dto.FornecedorNome, dto.DataPrevista, dto.Observacoes);

        // S17 FIX: remove os itens antigos do banco via DELETE direto...
        await _uow.PedidosCompra.RemoverItensAsync(id);

        // ...e da coleção em memória, ANTES de chamar Update(pedido) — se Update()
        // rodasse com os itens antigos ainda presentes em pedido.Itens, ele varreria
        // o grafo e marcaria esses itens (já deletados pela linha acima, por fora do
        // change tracker) como Modified, e o SaveChanges tentaria fazer UPDATE numa
        // linha que não existe mais ("esperava afetar 1, afetou 0" de novo).
        pedido.Itens.Clear();

        _uow.PedidosCompra.Update(pedido);

        var novosItens = dto.Itens.Select(item => new PedidoCompraItem
        {
            PedidoCompraId = id,
            ProductId      = item.ProductId,
            ProductName    = item.ProductName,
            Quantidade     = item.Quantidade,
            PrecoUnitario  = item.PrecoUnitario,
            CreatedAt      = DateTime.UtcNow,
        }).ToList();

        await _uow.PedidosCompra.AdicionarItensAsync(novosItens);
        await _uow.CommitAsync();
    }

    public async Task EnviarAsync(Guid id)
    {
        // GetWithItensAsync — obrigatório para que pedido.Itens.Any() funcione
        var pedido = await _uow.PedidosCompra.GetWithItensAsync(id)
            ?? throw new KeyNotFoundException($"Pedido {id} não encontrado.");

        pedido.Enviar();
        // Update necessário: GetWithItensAsync usa FirstOrDefaultAsync com NoTracking global
        // sem isso o EF não detecta a mudança de Status e CommitAsync salva nada
        _uow.PedidosCompra.Update(pedido);
        await _uow.CommitAsync();
    }

    public async Task ReceberAsync(Guid id)
    {
        var pedido = await _uow.PedidosCompra.GetWithItensAsync(id)
            ?? throw new KeyNotFoundException($"Pedido {id} não encontrado.");

        var itens = pedido.Receber();

        // 1. O SEGREDO: Dar o Update no pedido PRIMEIRO! 
        // Isso anexa o pedido e qualquer produto "fantasma" pendurado nele na memória,
        // garantindo que o Entity Framework rastreie a cópia certa.
        _uow.PedidosCompra.Update(pedido);

        // 2. Agora atualizamos o estoque
        // ── CORREÇÃO: GetByIdTrackedAsync garante rastreamento ───────────
        // GetByIdAsync retorna Detached (NoTracking global), e como NÃO
        // chamamos Update() aqui, a mutação ficava invisível ao EF.
        // Com AsTracking(), o ChangeTracker detecta as mudanças e gera
        // UPDATE + AuditLog automaticamente no CommitAsync().
       foreach (var item in itens)
        {
            var produto = await _uow.Products.GetByIdTrackedAsync(item.ProductId);
            if (produto is null) continue;

            produto.Stock += item.Quantidade;
            
            // A vacina dupla do custo!
            if (item.PrecoUnitario > 0)
            {
                produto.OriginalCost = item.PrecoUnitario;
                produto.CostPrice = item.PrecoUnitario;
                
                produto.CostPriceChangedAt = DateTime.UtcNow;
                produto.CostPriceChangedBy = "Sistema (Entrada de Compras)";
            }
        }

        await _uow.CommitAsync();
    }

    public async Task CancelarAsync(Guid id)
    {
        var pedido = await _uow.PedidosCompra.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Pedido {id} não encontrado.");

        pedido.Cancelar();
        _uow.PedidosCompra.Update(pedido);
        await _uow.CommitAsync();
    }

    public async Task DeletarAsync(Guid id)
    {
        var pedido = await _uow.PedidosCompra.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Pedido {id} não encontrado.");

        _uow.PedidosCompra.Remove(pedido);
        await _uow.CommitAsync();
    }

    private static PedidoCompraDto MapToDto(PedidoCompra p) => new()
    {
        Id              = p.Id,
        Numero          = p.Numero,
        FornecedorNome  = p.FornecedorNome,
        SupplierId      = p.SupplierId,
        DataPedido      = p.DataPedido,
        DataPrevista    = p.DataPrevista,
        DataRecebimento = p.DataRecebimento,
        Status          = p.Status,
        Observacoes     = p.Observacoes,
        Total           = p.Total,
        Itens           = p.Itens.Select(i => new PedidoCompraItemDto
        {
            Id            = i.Id,
            ProductId     = i.ProductId,
            ProductName   = i.ProductName,
            Quantidade    = i.Quantidade,
            PrecoUnitario = i.PrecoUnitario,
        }).ToList()
    };

    public async Task<IReadOnlyList<HistoricoCompraProdutoDto>> GetHistoricoPorProdutoAsync(Guid productId)
    {
        var itens = await _uow.PedidosCompra.GetHistoricoPorProdutoAsync(productId);
        return itens.Select(i => new HistoricoCompraProdutoDto
        {
            DataPedido     = i.PedidoCompra.DataPedido,
            NumeroPedido   = i.PedidoCompra.Numero,
            FornecedorNome = i.PedidoCompra.FornecedorNome,
            Quantidade     = i.Quantidade,
            PrecoUnitario  = i.PrecoUnitario,
            Status         = i.PedidoCompra.Status.ToString()
        }).ToList();
    }
}