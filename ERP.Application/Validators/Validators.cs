using ERP.Application.DTOs;
using FluentValidation;

namespace ERP.Application.Validators;

public class CreateProductValidator : AbstractValidator<CreateProductDto>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Nome é obrigatório.");
        RuleFor(x => x.Unit).NotEmpty().WithMessage("Unidade é obrigatória.");
        RuleFor(x => x.SalePrice).GreaterThan(0).WithMessage("Preço de venda deve ser maior que zero.");
        RuleFor(x => x.OriginalCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IpiPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.IcmsPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.NCM).MaximumLength(10).When(x => x.NCM != null);
    }
}

public class CreateCustomerValidator : AbstractValidator<CreateCustomerDto>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Nome é obrigatório.");
        
        // 👇 Valida o Documento APENAS se o usuário digitou alguma coisa
        When(x => !string.IsNullOrWhiteSpace(x.Document), () => 
        {
            RuleFor(x => x.Document)
                .Must(d => d.Replace(".", "").Replace("-", "").Replace("/", "").Length is 11 or 14)
                .WithMessage("CPF deve ter 11 dígitos ou CNPJ 14 dígitos.");
        });
    }
}

public class CreateSaleValidator : AbstractValidator<CreateSaleDto>
{
    public CreateSaleValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Venda deve ter pelo menos um item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Quantidade inválida.");
            item.RuleFor(i => i.UnitPrice).GreaterThan(0).WithMessage("Preço inválido.");
        });
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
    }
}
