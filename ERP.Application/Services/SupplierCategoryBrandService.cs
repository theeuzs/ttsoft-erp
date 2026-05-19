using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

// ── SupplierService ───────────────────────────────────────────────────────
public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _uow;
    public SupplierService(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<SupplierDto>> GetAllAsync()
    {
        var list = await _uow.Suppliers.GetAllAsync();
        return list.OrderBy(s => s.Name)
                   .Select(s => new SupplierDto(s.Id, s.Name));
    }

    public async Task<SupplierDto> CreateAsync(string name)
    {
        var entity = new Supplier { Name = name };
        await _uow.Suppliers.AddAsync(entity);
        await _uow.CommitAsync();
        return new SupplierDto(entity.Id, entity.Name);
    }
}

// ── CategoryService ───────────────────────────────────────────────────────
public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    public CategoryService(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        var list = await _uow.Categories.GetAllAsync();
        return list.OrderBy(c => c.Name)
                   .Select(c => new CategoryDto(c.Id, c.Name));
    }

    public async Task<CategoryDto> CreateAsync(string name)
    {
        var entity = new Category { Name = name };
        await _uow.Categories.AddAsync(entity);
        await _uow.CommitAsync();
        return new CategoryDto(entity.Id, entity.Name);
    }
}

// ── BrandService ──────────────────────────────────────────────────────────
public class BrandService : IBrandService
{
    private readonly IUnitOfWork _uow;
    public BrandService(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<BrandDto>> GetAllAsync()
    {
        var list = await _uow.Brands.GetAllAsync();
        return list.OrderBy(b => b.Name)
                   .Select(b => new BrandDto(b.Id, b.Name));
    }

    public async Task<BrandDto> CreateAsync(string name)
    {
        var entity = new Brand { Name = name };
        await _uow.Brands.AddAsync(entity);
        await _uow.CommitAsync();
        return new BrandDto(entity.Id, entity.Name);
    }
}

// ── UserQueryService ──────────────────────────────────────────────────────
public class UserQueryService : IUserQueryService
{
    private readonly IUnitOfWork _uow;
    public UserQueryService(IUnitOfWork uow) => _uow = uow;

    public async Task<IEnumerable<string>> GetAllNamesAsync()
    {
        var users = await _uow.Users.GetAllAsync();
        return users.OrderBy(u => u.Name).Select(u => u.Name);
    }
}