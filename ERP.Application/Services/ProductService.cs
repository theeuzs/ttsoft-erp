using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentValidation;
using System.Linq.Expressions;

namespace ERP.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateProductDto> _validator;

    public ProductService(IUnitOfWork uow, IMapper mapper, IValidator<CreateProductDto> validator)
    {
        _uow = uow;
        _mapper = mapper;
        _validator = validator;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
        => _mapper.Map<IEnumerable<ProductDto>>(await _uow.Products.GetAllAsync());

    public async Task<PagedResult<ProductDto>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null)
    {
        var (items, total) = await _uow.Products.GetPagedAsync(
            page:     page,
            pageSize: pageSize,
            // Achado (11/09) — buscava a frase inteira como um bloco só
            // (Contains(search)), diferente do WPF (que já divide em
            // palavras e exige todas, em qualquer ordem/lugar). Uma
            // vendedora buscou "não mais pregos" e só achou o produto cujo
            // nome começa exatamente assim — "CASCOLA NAO MAIS PREGOS 85G"
            // não batia, porque a frase inteira não aparecia como bloco
            // contíguo depois de "CASCOLA ". Agora usa a mesma lógica
            // palavra-por-palavra do ProductRepository.SearchAsync (WPF).
            filter:   string.IsNullOrWhiteSpace(search)
                        ? null
                        : ConstruirFiltroBuscaPorPalavras(search),
            orderBy:  p => p.Name);

        return new PagedResult<ProductDto>
        {
            Items      = _mapper.Map<IEnumerable<ProductDto>>(items),
            TotalItems = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    /// <summary>Monta, em árvore de expressão, o mesmo filtro "E de cada
    /// palavra" que o ProductRepository.SearchAsync (WPF) usa — cada palavra
    /// precisa bater em Nome (substring) OU Barcode/SKU (começa com),
    /// combinadas com E entre si. Construído manualmente porque
    /// GetPagedAsync só aceita UM filtro — isso vira um filtro só, com N
    /// condições ANDadas dentro.</summary>
    private static Expression<Func<Product, bool>> ConstruirFiltroBuscaPorPalavras(string search)
    {
        var palavras  = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parametro = Expression.Parameter(typeof(Product), "p");
        Expression? corpo = null;

        var nomeProp    = Expression.Property(parametro, nameof(Product.Name));
        var barcodeProp = Expression.Property(parametro, nameof(Product.Barcode));
        var skuProp     = Expression.Property(parametro, nameof(Product.SKU));
        var nulo        = Expression.Constant(null, typeof(string));
        var containsMi  = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var startsMi    = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

        foreach (var palavra in palavras)
        {
            var valor = Expression.Constant(palavra);

            var nomeContem     = Expression.Call(nomeProp, containsMi, valor);
            var barcodeComeca  = Expression.AndAlso(Expression.NotEqual(barcodeProp, nulo), Expression.Call(barcodeProp, startsMi, valor));
            var skuComeca      = Expression.AndAlso(Expression.NotEqual(skuProp, nulo), Expression.Call(skuProp, startsMi, valor));

            var condicaoPalavra = Expression.OrElse(Expression.OrElse(nomeContem, barcodeComeca), skuComeca);
            corpo = corpo == null ? condicaoPalavra : Expression.AndAlso(corpo, condicaoPalavra);
        }

        return Expression.Lambda<Func<Product, bool>>(corpo!, parametro);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id)
    {
        var product = await _uow.Products.GetByIdAsync(id);
        return product is null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<IEnumerable<ProductDto>> SearchAsync(string term)
        => _mapper.Map<IEnumerable<ProductDto>>(await _uow.Products.SearchAsync(term));

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        var product = await _uow.Products.GetByBarcodeAsync(barcode);
        return product is null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku)
    {
        var product = await _uow.Products.GetBySkuAsync(sku);
        return product is null ? null : _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        
        // CORREÇÃO: Criação explícita do contexto evita NRE no Mock
        var context = new ValidationContext<CreateProductDto>(dto);
        var validationResult = await _validator.ValidateAsync(context);
        
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var product = _mapper.Map<Product>(dto);
        await _uow.Products.AddAsync(product);
        await _uow.CommitAsync();
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> UpdateAsync(UpdateProductDto dto)
    {
        var product = await _uow.Products.GetByIdTrackedAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Produto {dto.Id} não encontrado.");

        bool salePriceChanged = product.SalePrice    != dto.SalePrice;
        bool costPriceChanged = product.OriginalCost != dto.OriginalCost;

        _mapper.Map(dto, product);

        product.UpdatedAt = DateTime.UtcNow;

        string? operador = ERP.Domain.CurrentUser.Name;
        if (salePriceChanged)
        {
            product.SalePriceChangedAt = DateTime.UtcNow; 
            product.SalePriceChangedBy = operador;
        }
        if (costPriceChanged)
        {
            product.CostPriceChangedAt = DateTime.UtcNow; 
            product.CostPriceChangedBy = operador;
        }

        await _uow.CommitAsync();
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<int> GetLowStockCountAsync()
    {
        var list = await _uow.Products.GetLowStockAsync();
        return list.Count();
    }

    public async Task<IEnumerable<ProductDto>> GetLowStockListAsync()
    {
        var list = await _uow.Products.GetLowStockAsync();
        return list.Select(p => _mapper.Map<ProductDto>(p));
    }

    public async Task DeleteAsync(Guid id)
    {
        var product = await _uow.Products.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Produto {id} não encontrado.");
        product.IsDeleted = true;
        product.IsActive  = false;
        _uow.Products.Update(product);
        await _uow.CommitAsync();
    }
}