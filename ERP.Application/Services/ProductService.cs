using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentValidation;

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
        // Filtro opcional por nome, código de barras ou SKU
        var (items, total) = await _uow.Products.GetPagedAsync(
            page:     page,
            pageSize: pageSize,
            filter:   string.IsNullOrWhiteSpace(search)
                        ? null
                        : p => p.Name.Contains(search) ||
                               (p.Barcode != null && p.Barcode.Contains(search)) ||
                               (p.SKU     != null && p.SKU.Contains(search)),
            orderBy:  p => p.Name);

        return new PagedResult<ProductDto>
        {
            Items      = _mapper.Map<IEnumerable<ProductDto>>(items),
            TotalItems = total,
            Page       = page,
            PageSize   = pageSize
        };
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
        await _validator.ValidateAndThrowAsync(dto);
        var product = _mapper.Map<Product>(dto);
        await _uow.Products.AddAsync(product);
        await _uow.CommitAsync();
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> UpdateAsync(UpdateProductDto dto)
    {
        // 1. GetByIdTrackedAsync — força rastreamento independente do NoTracking global
        var product = await _uow.Products.GetByIdTrackedAsync(dto.Id)
            ?? throw new KeyNotFoundException($"Produto {dto.Id} não encontrado.");

        // Captura flags ANTES do mapper sobrescrever os valores
        bool salePriceChanged = product.SalePrice    != dto.SalePrice;
        bool costPriceChanged = product.OriginalCost != dto.OriginalCost;

        // 2. Mapper injeta os novos valores na entidade já rastreada
        //    O ChangeTracker detecta a mutação e marca como Modified automaticamente
        _mapper.Map(dto, product);

        // 3. NÃO chamar Update() — forçar o Update sobrescreve OriginalValues e quebra auditoria
        product.UpdatedAt = DateTime.UtcNow;

        string? operador = ERP.Domain.CurrentUser.Name;
        if (salePriceChanged)
        {
            product.SalePriceChangedAt = DateTime.Now;
            product.SalePriceChangedBy = operador;
        }
        if (costPriceChanged)
        {
            product.CostPriceChangedAt = DateTime.Now;
            product.CostPriceChangedBy = operador;
        }

        // 4. Commit — EF detecta o que mudou via ChangeTracker e salva apenas os campos alterados
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