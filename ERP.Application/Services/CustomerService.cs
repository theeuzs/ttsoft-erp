using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using FluentValidation;

namespace ERP.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateCustomerDto> _validator;

    public CustomerService(IUnitOfWork uow, IMapper mapper, IValidator<CreateCustomerDto> validator)
        => (_uow, _mapper, _validator) = (uow, mapper, validator);

    public async Task<IEnumerable<CustomerDto>> GetAllAsync()
        => _mapper.Map<IEnumerable<CustomerDto>>(await _uow.Customers.GetAllAsync());

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null)
    {
        var (items, total) = await _uow.Customers.GetPagedAsync(
            page:     page,
            pageSize: pageSize,
            filter:   string.IsNullOrWhiteSpace(search)
                        ? null
                        : c => c.Name.Contains(search) ||
                               (c.Document != null && c.Document.Contains(search)) ||
                               (c.Phone    != null && c.Phone.Contains(search)),
            orderBy:  c => c.Name);

        return new PagedResult<CustomerDto>
        {
            Items      = _mapper.Map<IEnumerable<CustomerDto>>(items),
            TotalItems = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        var c = await _uow.Customers.GetByIdAsync(id);
        return c is null ? null : _mapper.Map<CustomerDto>(c);
    }

    public async Task<IEnumerable<CustomerDto>> SearchAsync(string term)
        => _mapper.Map<IEnumerable<CustomerDto>>(await _uow.Customers.SearchAsync(term));

    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        await _validator.ValidateAndThrowAsync(dto);

        Customer? existingCustomer = null;
        if (!string.IsNullOrWhiteSpace(dto.Document))
            existingCustomer = await _uow.Customers.GetByDocumentAsync(dto.Document);

        if (existingCustomer != null)
        {
            // GetByDocumentAsync usa AsNoTracking — rebusca com rastreamento explícito
            var rastreado = await _uow.Customers.GetByIdTrackedAsync(existingCustomer.Id)
                ?? existingCustomer;

            // Mapper injeta novos valores na entidade rastreada sem chamar Update()
            _mapper.Map(dto, rastreado);
            rastreado.IsDeleted = false;
            rastreado.Name      = dto.Name;
            rastreado.UpdatedAt = DateTime.UtcNow;

            await _uow.CommitAsync();
            return _mapper.Map<CustomerDto>(rastreado);
        }

        var entity = new Customer
        {
            Name              = dto.Name,
            Document          = string.IsNullOrWhiteSpace(dto.Document) ? null : dto.Document,
            Phone             = dto.Phone,
            Email             = dto.Email,
            StateRegistration = dto.StateRegistration,
            ZipCode           = dto.ZipCode,
            Street            = dto.Street,
            Number            = dto.Number,
            Complement        = dto.Complement,
            Neighborhood      = dto.Neighborhood,
            City              = dto.City,
            State             = dto.State,
            GrupoPreco        = (ERP.Domain.Enums.GrupoPreco)dto.GrupoPreco,
            LimiteCredito     = dto.LimiteCredito
        };

        await _uow.Customers.AddAsync(entity);
        await _uow.CommitAsync();
        return _mapper.Map<CustomerDto>(entity);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, CreateCustomerDto dto)
    {
        // 1. GetByIdTrackedAsync — força rastreamento independente do NoTracking global
        var existente = await _uow.Customers.GetByIdTrackedAsync(id)
            ?? throw new KeyNotFoundException($"Cliente {id} não encontrado.");

        // 2. Mapper injeta novos valores na entidade rastreada — ChangeTracker detecta mutação
        // 3. NÃO chamar Update() — sobrescreveria OriginalValues e quebraria auditoria
        _mapper.Map(dto, existente);
        existente.UpdatedAt = DateTime.UtcNow;

        // 4. Commit — EF salva apenas o que mudou via ChangeTracker
        await _uow.CommitAsync();
        return _mapper.Map<CustomerDto>(existente);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _uow.Customers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Cliente {id} não encontrado.");
        entity.IsDeleted = true;
        _uow.Customers.Update(entity);
        await _uow.CommitAsync();
    }
}