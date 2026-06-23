using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class DevolucaoService : IDevolucaoService
{
    private readonly IUnitOfWork    _uow;
    private readonly IHaverService  _haverService;
    private readonly IRequestTenant _tenant;

    public DevolucaoService(IUnitOfWork uow, IHaverService haverService, IRequestTenant tenant)
    {
        _uow          = uow;
        _haverService = haverService;
        _tenant       = tenant;
    }

    public async Task<decimal> GetQuantidadeJaDevolvida(Guid saleId, Guid productId)
        => await _uow.Devolucoes.GetQuantidadeJaDevolvida(saleId, productId);

    public async Task<DevolucaoResultDto> DevolverItensAsync(CreateDevolucaoDto dto)
    {
        if (!dto.Itens.Any(i => i.QuantidadeDevolver > 0))
            throw new InvalidOperationException("Selecione pelo menos um item para devolver.");

        var venda = await _uow.Sales.GetWithItemsAsync(dto.VendaId)
            ?? throw new KeyNotFoundException("Venda não encontrada.");

        if (venda.Status == Domain.Enums.SaleStatus.Cancelada)
            throw new InvalidOperationException("Não é possível devolver itens de uma venda cancelada.");

        var itensValidos = dto.Itens.Where(i => i.QuantidadeDevolver > 0).ToList();

        // ══════════════════════════════════════════════════════════════════
        // VALIDAÇÃO ANTI-EXPLOIT: verifica quantidade já devolvida no banco
        // Impede que devoluções cumulativas ultrapassem a quantidade vendida
        // ══════════════════════════════════════════════════════════════════
        foreach (var item in itensValidos)
        {
            var itemVenda = venda.Items.FirstOrDefault(i => i.ProductId == item.ProductId)
                ?? throw new KeyNotFoundException($"Produto '{item.ProductName}' não encontrado na venda.");

            decimal jaDevolvido = await _uow.Devolucoes.GetQuantidadeJaDevolvida(dto.VendaId, item.ProductId);
            decimal disponivelParaDevolucao = itemVenda.Quantity - jaDevolvido;

            if (item.QuantidadeDevolver > disponivelParaDevolucao)
                throw new InvalidOperationException(
                    $"Não é possível devolver {item.QuantidadeDevolver:N2} de '{item.ProductName}'. " +
                    $"Quantidade disponível para devolução: {disponivelParaDevolucao:N2} " +
                    $"(vendido: {itemVenda.Quantity:N2}, já devolvido: {jaDevolvido:N2}).");
        }

        // 2. Devolve estoque de forma atômica
        foreach (var item in itensValidos)
        {
            await _uow.Products.BaixarEstoqueAtomicoAsync(
                item.ProductId, -item.QuantidadeDevolver, allowNegative: true);
        }

        // S8 FIX: OperadorNome do JWT — não confiar no body (trilha de auditoria com repúdio).
        var operadorNome = _tenant.UserName;

        // S8 FIX: valorTotal recalculado no servidor usando preços da venda original.
        // Antes: dto.Items[].ValorTotal vinha do cliente → fraude de haver (ex.: 1 un → R$ 9.999.999 de crédito).
        // Agora: UnitPrice × (1 − DiscountPercent/100) × QuantidadeDevolver, tudo da venda original.
        decimal valorTotal = 0m;

        // 3. Registra cada item devolvido para controle futuro (anti-exploit)
        foreach (var item in itensValidos)
        {
            var itemVenda        = venda.Items.First(i => i.ProductId == item.ProductId);
            var unitPriceEfetivo = itemVenda.UnitPrice * (1m - itemVenda.DiscountPercent / 100m);
            var valorItem        = unitPriceEfetivo * item.QuantidadeDevolver;
            valorTotal          += valorItem;

            await _uow.Devolucoes.AddAsync(new Domain.Entities.SaleItemDevolucao
            {
                SaleId              = dto.VendaId,
                ProductId           = item.ProductId,
                ProductName         = item.ProductName,
                QuantidadeDevolvida = item.QuantidadeDevolver,
                ValorDevolvido      = valorItem,     // ← preço autoritativo
                Motivo              = dto.Motivo,
                OperadorNome        = operadorNome,  // ← JWT
                DataDevolucao       = DateTime.Now,
            });
        }

        // 4. Credita Haver ao cliente
        if (dto.CustomerId.HasValue && valorTotal > 0)
        {
            string descricao = $"Devolução parcial - Venda {venda.SaleNumber ?? dto.VendaId.ToString()[..8].ToUpper()}";
            if (!string.IsNullOrWhiteSpace(dto.Motivo))
                descricao += $" ({dto.Motivo})";

            await _haverService.RegistrarMovimentoVendaAsync(
                dto.CustomerId.Value, valorTotal, "Entrada",
                descricao, operadorNome); // ← JWT

            var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId.Value);
            if (customer != null)
            {
                customer.HaverBalance += valorTotal;
                _uow.Customers.Update(customer);
            }
        }

        // 5. Salva Customer (HaverBalance) — o Devolucao já foi inserido via SQL direto
        //    e o MovimentoHaver foi salvo pelo HaverService internamente
        await _uow.CommitAsync();

        string nomeCliente = "Consumidor Final";
        if (dto.CustomerId.HasValue)
        {
            var c = await _uow.Customers.GetByIdAsync(dto.CustomerId.Value);
            nomeCliente = c?.Name ?? nomeCliente;
        }

        return new DevolucaoResultDto(
            ValorTotalDevolvido:  valorTotal,
            NumeroVendaOriginal:  venda.SaleNumber ?? dto.VendaId.ToString()[..8].ToUpper(),
            NomeCliente:          nomeCliente,
            ItensDevolvidos:      itensValidos);
    }
}