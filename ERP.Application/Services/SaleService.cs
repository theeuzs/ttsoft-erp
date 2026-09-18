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
    private readonly IEstoqueSyncService? _estoqueSync;
    private readonly ISalePolicyService? _salePolicy;
    private readonly IRequestTenant _tenant;

    public SaleService(IUnitOfWork uow, IMapper mapper, IValidator<CreateSaleDto> validator,
                       IHaverService haverService, IRequestTenant tenant,
                       IFidelidadeService? fidelidade = null,
                       ISalePolicyService? salePolicy = null,
                       IEstoqueSyncService? estoqueSync = null)
    {
        _uow          = uow;
        _mapper       = mapper;
        _validator    = validator;
        _haverService = haverService;
        _tenant       = tenant;
        _fidelidade   = fidelidade;
        _salePolicy   = salePolicy;
        _estoqueSync  = estoqueSync;
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

        if (dto.Id.HasValue)
        {
            var existente = await _uow.Sales.GetByIdAsync(dto.Id.Value);
            if (existente != null)
                return _mapper.Map<SaleDto>(existente);
        }

        var exigeCaixa = _salePolicy?.RequerCaixaAberto(dto.Origem) ?? true;
        if (exigeCaixa)
        {
            var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(dto.UsuarioId);
            if (caixaAberto == null)
                throw new InvalidOperationException("Não é possível realizar vendas: O CAIXA ESTÁ FECHADO.");
        }

        var novaVendaId = dto.Id ?? Guid.NewGuid();

        var sale = new Sale
        {
            Id = novaVendaId, 
            SaleNumber = GenerateSaleNumber(),
            Origem = dto.Origem,
            CustomerId = dto.CustomerId,
            SellerName = dto.SellerName,
            DiscountAmount = dto.DiscountAmount,
            ShippingValue = dto.ShippingValue,
            SaleDate = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil(),
            Notes = dto.Notes,
            Payments = dto.Payments.Select(p => new SalePayment
            {
                Id = p.Id ?? Guid.NewGuid(),
                SaleId = novaVendaId,
                PaymentMethod = p.PaymentMethod,
                Amount = p.Amount
            }).ToList()
        };

        var produtosComEstoqueAlterado = new HashSet<Guid>();

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            produtosComEstoqueAlterado.Clear();
            sale.Items.Clear();
            await using var tx = await _uow.BeginTransactionAsync();
            try
            {
                var grupoPreco = ERP.Domain.Enums.GrupoPreco.A;
                if (dto.CustomerId.HasValue)
                {
                    var clienteGrupo = await _uow.Customers.GetByIdAsync(dto.CustomerId.Value);
                    if (clienteGrupo != null)
                        grupoPreco = clienteGrupo.GrupoPreco;
                }

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

                    if (baixouOk)
                        produtosComEstoqueAlterado.Add(produtoEstoque.Id);

                    if (!baixouOk)
                        throw new InvalidOperationException(
                            $"Estoque insuficiente para '{produtoEstoque.Name}' " +
                            $"(necessário: {qtdEstoque:N2}, disponível no estoque). " +
                            $"Outro terminal pode ter vendido o último item agora mesmo.");

                    var (descontoOk, descontoErro) = ERP.Application.Helpers.DescontoPolicy.Validar(
                        itemDto.DiscountPercent,
                        _tenant.MaxDiscountPercentage,
                        product.Name);
                    if (!descontoOk) throw new InvalidOperationException(descontoErro!);

                    var unitPriceNormal = product.GetPrecoParaGrupo(grupoPreco);

                    bool ehAtacado = product.WholesalePrice.HasValue
                        && product.WholesaleMinQuantity.HasValue
                        && itemDto.Quantity >= product.WholesaleMinQuantity.Value;

                    decimal unitPrice;
                    decimal totalItem;

                    if (ehAtacado)
                    {
                        var (totalAtacado, precoEquivalente) = ERP.Application.Helpers.DescontoPolicy.CalcularTotalAtacado(
                            itemDto.Quantity, product.WholesaleMinQuantity!.Value, product.WholesalePrice!.Value,
                            unitPriceNormal, itemDto.DiscountPercent);

                        totalItem = totalAtacado;
                        unitPrice = precoEquivalente;
                    }
                    else
                    {
                        unitPrice = unitPriceNormal;
                        totalItem = ERP.Application.Helpers.DescontoPolicy.CalcularTotal(
                            unitPrice, itemDto.Quantity, itemDto.DiscountPercent);
                    }

                    sale.Items.Add(new SaleItem
                    {
                        Id              = Guid.NewGuid(),
                        SaleId          = novaVendaId,
                        ProductId       = product.Id,
                        ProductName     = product.Name,
                        Quantity        = qtdEstoque,
                        UnitPrice       = unitPrice,
                        DiscountPercent = itemDto.DiscountPercent,
                        TotalItem       = totalItem
                    });
                }

                sale.RecalculateTotals();

                var valorAPrazo = dto.Payments
                    .Where(p => p.PaymentMethod == Domain.Enums.PaymentMethod.APrazo)
                    .Sum(p => p.Amount);

                if (valorAPrazo > 0 && dto.CustomerId.HasValue && !dto.AutorizadoPorGerente)
                {
                    var clienteCredito = await _uow.Customers.GetByIdAsync(dto.CustomerId.Value);
                    if (clienteCredito != null && clienteCredito.LimiteCredito > 0)
                    {
                        var saldoDevedorAtual = await _uow.ContasReceber.GetSaldoDevedorAtualAsync(dto.CustomerId.Value);
                        if (saldoDevedorAtual + valorAPrazo > clienteCredito.LimiteCredito)
                            throw new ERP.Application.Exceptions.LimiteCreditoExcedidoException(
                                clienteCredito.Name, clienteCredito.LimiteCredito, saldoDevedorAtual, valorAPrazo);
                    }
                }

                var pagamentoHaver = dto.Payments.FirstOrDefault(p => p.PaymentMethod == Domain.Enums.PaymentMethod.Haver);
                if (pagamentoHaver != null && dto.CustomerId.HasValue)
                {
                    bool debitouOk = await _uow.Customers.DebitarHaverAtomicoAsync(
                        dto.CustomerId.Value, pagamentoHaver.Amount);

                    if (!debitouOk)
                        throw new InvalidOperationException(
                            "Saldo haver insuficiente (ou outra operação debitou o saldo agora mesmo).");
                }

                await _uow.Sales.AddAsync(sale);
                await _uow.CommitAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                throw new Exception($"ERRO NA VENDA (revertido): {ex.InnerException?.Message ?? ex.Message}", ex);
            }
        });
        
        if (sale.CustomerId.HasValue && _fidelidade != null)
        {
            try { await _fidelidade.AcumularPontosAsync(sale.CustomerId.Value, sale.Id, sale.Total); }
            catch (Exception exFid)
            {
                System.Diagnostics.Debug.WriteLine($"[FIDELIDADE ERRO] {exFid.Message} | Inner: {exFid.InnerException?.Message}");
                Serilog.Log.Warning(exFid, "Erro ao acumular pontos de fidelidade para cliente {CustomerId}", sale.CustomerId);
            }
        }

        if (_estoqueSync != null)
            foreach (var productId in produtosComEstoqueAlterado)
                await _estoqueSync.SincronizarProdutoAsync(productId);

        return _mapper.Map<SaleDto>(sale);
    }
    public async Task AtualizarDadosNfceAsync(Guid vendaId, string urlDanfe, string status, string ambiente, string referencia)
{
    var venda = await _uow.Sales.GetByIdAsync(vendaId); 
    
    if (venda != null)
    {
        venda.NfceUrlDanfe = urlDanfe;
        venda.NfceStatusFocus = status;
        venda.NfceAmbiente = ambiente;
        venda.NfceReferencia = referencia;

        if (status == "Autorizada")
            venda.Status = Domain.Enums.SaleStatus.NotaEmitida;
        
        _uow.Sales.Update(venda);
        await _uow.CommitAsync();
    }
}

public async Task<IEnumerable<SalesReportItemDto>> GetSalesReportAsync(DateTime startDate, DateTime endDate, string? sellerName = null)
    {
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
                pagamentoStr = s.Payments.First().PaymentMethod.ToString();
            }

            return new SalesReportItemDto
            {
                DataVenda = s.SaleDate, 
                NumeroRecibo = s.SaleNumber ?? s.Id.ToString().Substring(0, 8).ToUpper(), 
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

        var itensAgrupados = sale.Items.GroupBy(i => i.ProductId);
        
        foreach (var grupo in itensAgrupados)
        {
            var product = await _uow.Products.GetByIdAsync(grupo.Key);
            if (product != null)
            {
                decimal quantidadeTotalDevolvida = grupo.Sum(i => i.Quantity);
                product.Stock += quantidadeTotalDevolvida;
                
                _uow.Products.Update(product);
            }
        }

        var pagamentoHaver = sale.Payments?.FirstOrDefault(p => p.PaymentMethod == Domain.Enums.PaymentMethod.Haver);
        
        if (pagamentoHaver != null && sale.CustomerId.HasValue)
        {
            var customer = await _uow.Customers.GetByIdAsync(sale.CustomerId.Value);
            if (customer != null)
            {
                customer.HaverBalance += pagamentoHaver.Amount;
                _uow.Customers.Update(customer); 

                if (_haverService == null)
                {
                    throw new Exception("Ei! O _haverService está nulo. Verifique se você colocou '_haverService = haverService;' dentro do construtor do SaleService.");
                }

                string descricaoEstorno = $"Estorno de Cancelamento - Venda {(string.IsNullOrWhiteSpace(sale.SaleNumber) ? sale.Id.ToString().Substring(0, 8).ToUpper() : sale.SaleNumber)}";
                
                await _haverService.RegistrarMovimentoVendaAsync(customer.Id, pagamentoHaver.Amount, "Entrada", descricaoEstorno, "Sistema");
            }
        }

        // Achado (11/09) — cancelar venda nunca revertia o movimento de
        // CAIXA. Pra cada forma de pagamento (menos Haver, já revertido
        // acima), acha o lançamento original e cria um estorno com valor
        // negativo.
        //
        // Achado (17/09) — bug real, achado num cancelamento de verdade na
        // Vila Verde: "Cannot insert duplicate key row... unique index
        // 'IX_CaixaMovimentos_SalePaymentId'". SalePaymentId = pagamento.Id
        // reusava o MESMO id que o lançamento ORIGINAL (feito na hora da
        // venda) já ocupa nesse índice único — a inserção do estorno sempre
        // colidia, pra QUALQUER venda com pagamento não-Haver, não só essa.
        // Esse índice existe pra impedir cobrar o MESMO pagamento duas vezes
        // (idempotência da cobrança original) — o estorno é um evento
        // diferente, não uma cobrança repetida, então não deveria competir
        // por aquele valor. SalePaymentId aqui vira null (coluna aceita,
        // índice único do SQL Server permite múltiplos null sem conflito) —
        // a rastreabilidade não se perde: Descricao e VendaId já deixam
        // claro o que está sendo estornado.
        if (sale.Payments != null)
        {
            foreach (var pagamento in sale.Payments.Where(p => p.PaymentMethod != Domain.Enums.PaymentMethod.Haver))
            {
                var movimentoOriginal = await _uow.Caixas.ObterMovimentoOriginalAsync(pagamento.Id);
                if (movimentoOriginal == null) continue;

                await _uow.Caixas.AddMovimentoAsync(new Domain.Entities.CaixaMovimento
                {
                    Id             = Guid.NewGuid(),
                    CaixaId        = movimentoOriginal.CaixaId,
                    Valor          = -pagamento.Amount,
                    Descricao      = $"Estorno de Cancelamento - Venda {(string.IsNullOrWhiteSpace(sale.SaleNumber) ? sale.Id.ToString().Substring(0, 8).ToUpper() : sale.SaleNumber)}",
                    FormaPagamento = pagamento.PaymentMethod,
                    Tipo           = Domain.Enums.TipoMovimentoCaixa.CancelamentoVenda,
                    DataHora       = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil(),
                    VendaId        = sale.Id,
                    SalePaymentId  = null
                });
            }
        }

        var contasReceber = await _uow.ContasReceber.GetBySaleIdAsync(id)?? new List<ContaReceber>();
        foreach (var conta in contasReceber.Where(c => c.Status == "Pendente"))
        {
            conta.Status    = "Cancelado";
            conta.UpdatedAt = DateTime.UtcNow;
            _uow.ContasReceber.Update(conta);
        }

        sale.Cancel(reason);
        sale.Status = Domain.Enums.SaleStatus.Cancelada;
        
        sale.Customer = null; 
        foreach (var item in sale.Items)
        {
            item.Product = null;
        }
        
        _uow.Sales.Update(sale);
        
        await _uow.CommitAsync();
    }
    private static readonly ThreadLocal<Random> _rng =
        new(() => new Random(Guid.NewGuid().GetHashCode()));

    private static string GenerateSaleNumber()
        => $"VND{DateTime.UtcNow:yyyyMMddHHmmssfff}{_rng.Value!.Next(100, 999)}";
}