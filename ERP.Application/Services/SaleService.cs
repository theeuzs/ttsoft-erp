using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Application.Services;

public class SaleService : ISaleService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateSaleDto> _validator;
    private readonly IHaverService _haverService;
    private readonly IFidelidadeService? _fidelidade;

    public SaleService(IUnitOfWork uow, IMapper mapper, IValidator<CreateSaleDto> validator, IHaverService haverService, IFidelidadeService? fidelidade = null)
    {
        _uow = uow;
        _mapper = mapper;
        _validator = validator;
        _haverService = haverService;
        _fidelidade   = fidelidade;
    }

    public async Task<IEnumerable<SaleDto>> GetAllAsync(DateTime? from = null, DateTime? to = null, string? sellerId = null)
    {
        var start = from ?? DateTime.Today.AddMonths(-1);
        var end = to ?? DateTime.Today.AddDays(1);
        var sales = await _uow.Sales.GetByDateRangeAsync(start, end);
        if (sellerId != null)
            sales = sales.Where(s => s.SellerId == sellerId);
        return _mapper.Map<IEnumerable<SaleDto>>(sales);
    }

    public async Task<SaleDetailDto?> GetDetailAsync(Guid id)
{
    // 1. Busca a venda com o Cliente incluído (O Repository já faz isso perfeitamente)
    var sale = await _uow.Sales.GetWithItemsAsync(id);
    if (sale == null) return null;

    // 2. O AutoMapper faz todo o trabalho duro e já traz o telefone
    return _mapper.Map<SaleDetailDto>(sale);
}

    public async Task<SaleDto> CreateAsync(CreateSaleDto dto)
    {
        await _validator.ValidateAndThrowAsync(dto);

        // 1. Bloqueia se o caixa estiver fechado
        var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(dto.UsuarioId);
        if (caixaAberto == null)
            throw new InvalidOperationException("Não é possível realizar vendas: O CAIXA ESTÁ FECHADO.");

        var novaVendaId = Guid.NewGuid();

        var sale = new Sale
        {
            Id = novaVendaId, 
            SaleNumber = GenerateSaleNumber(),
            CustomerId = dto.CustomerId,
            SellerName = dto.SellerName,
            DiscountAmount = dto.DiscountAmount,
            SaleDate = DateTime.Now,
            Notes = dto.Notes,
            Payments = dto.Payments.Select(p => new SalePayment
            {
                Id = Guid.NewGuid(), 
                SaleId = novaVendaId,
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount
            }).ToList()
        };

        // ── FASE 0 FIX: Transação única para baixa de estoque + criação da venda ──
        // Antes: BaixarEstoqueAtomico tinha commit próprio e _uow.CommitAsync()
        // vinha depois sem transação envolvendo os dois. Se o segundo commit falhasse
        // (queda de rede, constraint violation, etc.), o estoque já havia sido baixado
        // sem a venda correspondente → estoque fantasma garantido.
        //
        // Agora: ambas as operações estão dentro de uma única ITransaction.
        // Se qualquer parte falhar, o RollbackAsync desfaz tudo — incluindo o UPDATE
        // SQL do BaixarEstoqueAtomico, que usa a mesma conexão/transação do DbContext.
        //
        // NOTA para testes com InMemory: ITransaction/EfTransaction em InMemory é um no-op
        // (não há rollback real), mas o comportamento de negócio permanece correto.
        // Para testes de rollback real, use SQLite in-process (ver SaleServiceTests).

        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            // 2. Baixa de Estoque (atômica — segura em multi-terminal)
            foreach (var itemDto in dto.Items)
            {
                var product = await _uow.Products.GetByIdAsync(itemDto.ProductId)
                    ?? throw new KeyNotFoundException($"Produto {itemDto.ProductId} não encontrado.");

                Product produtoEstoque;
                decimal qtdEstoque;

                if (product.ParentProductId.HasValue && product.ConversionFactor > 0)
                {
                    produtoEstoque = await _uow.Products.GetByIdAsync(product.ParentProductId.Value)
                        ?? throw new KeyNotFoundException(
                            $"Produto pai de '{product.Name}' não encontrado. " +
                            $"Verifique o cadastro de produto composto.");

                    qtdEstoque = itemDto.Quantity * product.ConversionFactor;
                }
                else
                {
                    produtoEstoque = product;
                    qtdEstoque = itemDto.Quantity;
                }

                bool baixouOk = await _uow.Products.BaixarEstoqueAtomicoAsync(
                    produtoEstoque.Id, qtdEstoque, produtoEstoque.AllowNegativeStock);

                if (!baixouOk)
                    throw new InvalidOperationException(
                        $"Estoque insuficiente para '{produtoEstoque.Name}' " +
                        $"(necessário: {qtdEstoque:N2}, disponível no estoque). " +
                        $"Outro terminal pode ter vendido o último item agora mesmo.");

                sale.Items.Add(new SaleItem
                {
                    Id              = Guid.NewGuid(),
                    SaleId          = novaVendaId,
                    ProductId       = product.Id,
                    ProductName     = product.Name,
                    Quantity        = qtdEstoque,
                    UnitPrice       = itemDto.UnitPrice,
                    DiscountPercent = itemDto.DiscountPercent,
                    TotalItem       = itemDto.TotalItem
                });
            }

            sale.RecalculateTotals();

            // 3. Saldo em Haver
            var pagamentoHaver = dto.Payments.FirstOrDefault(p => p.PaymentMethod == Domain.Enums.PaymentMethod.Haver);
            if (pagamentoHaver != null && dto.CustomerId.HasValue)
            {
                var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId.Value);
                if (customer != null)
                {
                    if (customer.HaverBalance < pagamentoHaver.Amount)
                        throw new InvalidOperationException("Saldo haver insuficiente.");

                    customer.HaverBalance -= pagamentoHaver.Amount;
                    _uow.Customers.Update(customer);
                }
            }

            // 4. Persiste venda — dentro da mesma transação da baixa de estoque
            await _uow.Sales.AddAsync(sale);
            await _uow.CommitAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            // RollbackAsync desfaz tanto o CommitAsync do EF quanto o UPDATE SQL
            // do BaixarEstoqueAtomico — os dois estão na mesma transação.
            await tx.RollbackAsync();
            throw new Exception($"ERRO NA VENDA (revertido): {ex.InnerException?.Message ?? ex.Message}", ex);
        }
        
        // Sprint Q: acumular pontos de fidelidade se tiver cliente
        if (sale.CustomerId.HasValue && _fidelidade != null)
        {
            try { await _fidelidade.AcumularPontosAsync(sale.CustomerId.Value, sale.Id, sale.Total); }
            catch (Exception exFid)
            {
                System.Diagnostics.Debug.WriteLine($"[FIDELIDADE ERRO] {exFid.Message} | Inner: {exFid.InnerException?.Message}");
                Serilog.Log.Warning(exFid, "Erro ao acumular pontos de fidelidade para cliente {CustomerId}", sale.CustomerId);
            }
        }

        return _mapper.Map<SaleDto>(sale);
    }
    public async Task AtualizarDadosNfceAsync(Guid vendaId, string urlDanfe, string status, string ambiente, string referencia)
{
    // 👇 Mudamos de _repository para _uow.Sales 👇
    var venda = await _uow.Sales.GetByIdAsync(vendaId); 
    
    if (venda != null)
    {
        venda.NfceUrlDanfe = urlDanfe;
        venda.NfceStatusFocus = status;
        venda.NfceAmbiente = ambiente;
        venda.NfceReferencia = referencia;
        
        _uow.Sales.Update(venda);
        await _uow.CommitAsync(); // Se der erro de nome aqui, troque para _uow.SaveChangesAsync()
    }
}

public async Task<IEnumerable<SalesReportItemDto>> GetSalesReportAsync(DateTime startDate, DateTime endDate, string? sellerName = null)
    {
        // 1. Agora o banco já nos entrega tudo mastigado, cruzado e super rápido!
        var sales = await _uow.Sales.GetSalesByPeriodAsync(startDate, endDate);

        var query = sales.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(sellerName) && sellerName != "Todos")
        {
            query = query.Where(s => s.SellerName == sellerName);
        }

        return query.Select(s => 
        {
            string pagamentoStr = "Não Informado";
            
            if (s.Payments != null && s.Payments.Any())
            {
                // Como agora o banco trouxe os pagamentos de verdade, isso aqui vai funcionar 100% das vezes
                pagamentoStr = s.Payments.First().PaymentMethod.ToString();
            }

            return new SalesReportItemDto
            {
                DataVenda = s.SaleDate, 
                NumeroRecibo = s.SaleNumber ?? s.Id.ToString().Substring(0, 8).ToUpper(), 
                
                // O Include já conectou o cliente, não precisamos mais cruzar listas!
                ClienteNome = s.Customer?.Name ?? "Consumidor Final", 
                
                VendedorNome = s.SellerName ?? "Desconhecido",
                FormaPagamento = pagamentoStr, 
                ValorTotal = s.Total 
            };
        })
        .OrderByDescending(s => s.DataVenda)
        .ToList();
    }

    public async Task CancelAsync(Guid id, string reason)
    {
        var sale = await _uow.Sales.GetWithItemsAsync(id)
            ?? throw new KeyNotFoundException($"Venda {id} não encontrada.");

        if (sale.Status == Domain.Enums.SaleStatus.Cancelada)
            throw new InvalidOperationException("Venda já está cancelada.");

        // 1. DEVOLVE OS PRODUTOS PARA O ESTOQUE (Agrupado para evitar erro de Tracking)
        var itensAgrupados = sale.Items.GroupBy(i => i.ProductId);
        
        foreach (var grupo in itensAgrupados)
        {
            var product = await _uow.Products.GetByIdAsync(grupo.Key);
            if (product != null)
            {
                decimal quantidadeTotalDevolvida = grupo.Sum(i => i.Quantity);
                product.Stock += quantidadeTotalDevolvida;
                
                _uow.Products.Update(product); // 👈 Rastreado na mão direita do EF
            }
        }

        // 2. DEVOLVE O SALDO "HAVER" DO CLIENTE E GERA O HISTÓRICO
        // 👇 Colocamos a interrogação (?) caso o EF não tenha carregado os pagamentos
        var pagamentoHaver = sale.Payments?.FirstOrDefault(p => p.PaymentMethod == Domain.Enums.PaymentMethod.Haver);
        
        if (pagamentoHaver != null && sale.CustomerId.HasValue)
        {
            var customer = await _uow.Customers.GetByIdAsync(sale.CustomerId.Value);
            if (customer != null)
            {
                customer.HaverBalance += pagamentoHaver.Amount;
                _uow.Customers.Update(customer); 

                // 👇 Verificação de segurança para a injeção de dependência
                if (_haverService == null)
                {
                    throw new Exception("Ei! O _haverService está nulo. Verifique se você colocou '_haverService = haverService;' dentro do construtor do SaleService.");
                }

                string descricaoEstorno = $"Estorno de Cancelamento - Venda {(string.IsNullOrWhiteSpace(sale.SaleNumber) ? sale.Id.ToString().Substring(0, 8).ToUpper() : sale.SaleNumber)}";
                
                await _haverService.RegistrarMovimentoVendaAsync(customer.Id, pagamentoHaver.Amount, "Entrada", descricaoEstorno, "Sistema");
            }
        }

        // 3. CANCELA CONTAS A RECEBER VINCULADAS À VENDA
        var contasReceber = await _uow.ContasReceber.GetBySaleIdAsync(id)?? new List<ContaReceber>();
        foreach (var conta in contasReceber.Where(c => c.Status == "Pendente"))
        {
            conta.Status    = "Cancelado";
            conta.UpdatedAt = DateTime.UtcNow;
            _uow.ContasReceber.Update(conta);
        }

        // 4. CANCELA A VENDA NO BANCO
        sale.Cancel(reason);
        sale.Status = Domain.Enums.SaleStatus.Cancelada;
        
        // 👇 O SEGREDO ANTI-TRACKING 👇
        sale.Customer = null; 
        foreach (var item in sale.Items)
        {
            item.Product = null;
        }
        
        _uow.Sales.Update(sale); // 👈 Agora o EF atualiza só a venda sem puxar os penduricalhos!
        
        await _uow.CommitAsync();
    }
    private static string GenerateSaleNumber()
        => $"VND{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(100, 999)}";
}