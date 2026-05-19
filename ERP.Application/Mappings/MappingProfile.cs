using AutoMapper;
using ERP.Application.DTOs;
using ERP.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Product
        CreateMap<Product, ProductDto>()
            .ConstructUsing(p => new ProductDto(
                p.Id, p.Name, p.Barcode, p.SKU,
                p.Category != null ? p.Category.Name : null,
                p.Brand != null ? p.Brand.Name : null,
                p.Unit, p.SalePrice, p.Stock, p.MinStock, p.IsActive, p.EmCampanha, p.ImageUrl, p.DescricaoDetalhada))
            .AfterMap((src, dst) =>
            {
                dst.SalePriceChangedAt = src.SalePriceChangedAt;
                dst.SalePriceChangedBy = src.SalePriceChangedBy;
                dst.CostPriceChangedAt = src.CostPriceChangedAt;
                dst.CostPriceChangedBy = src.CostPriceChangedBy;
                dst.UnidadeEstoque     = src.UnidadeEstoque;
                dst.UnidadeVenda       = src.UnidadeVenda;
                dst.FatorConversao     = src.FatorConversao;
                dst.LabelUnidadeVenda  = src.LabelUnidadeVenda;
                dst.ParentProductId    = src.ParentProductId;
                dst.ConversionFactor   = src.ConversionFactor;
                dst.ParentProductName  = src.ParentProduct?.Name;
            });

        CreateMap<CreateProductDto, Product>()
            .ForMember(dst => dst.IsProdutoFilho, opt => opt.Ignore())
            .ForMember(dst => dst.UsaConversaoUnidade, opt => opt.Ignore())
            .ForMember(dst => dst.ParentProduct, opt => opt.Ignore());
        CreateMap<UpdateProductDto, Product>()
            .ForMember(dst => dst.SalePriceChangedAt, opt => opt.Ignore())
            .ForMember(dst => dst.SalePriceChangedBy, opt => opt.Ignore())
            .ForMember(dst => dst.CostPriceChangedAt,  opt => opt.Ignore())
            .ForMember(dst => dst.CostPriceChangedBy,  opt => opt.Ignore());

        // Customer
        CreateMap<Customer, CustomerDto>()
            .ConstructUsing(c => new CustomerDto(
                c.Id,
                c.Document,
                c.Name,
                c.Phone,
                c.City,
                c.HaverBalance,
                c.Street,
                c.Number,
                c.Neighborhood,
                c.State,
                c.ZipCode,
                c.StateRegistration,  // Ie
                c.Email,
                (int)c.GrupoPreco,
                c.LimiteCredito,
                c.SaldoDevedor
            ));
            
        CreateMap<CreateCustomerDto, Customer>();

        // Sale
        CreateMap<Sale, SaleDto>()
            .ConstructUsing(s => new SaleDto(
                s.Id,
                s.SaleNumber,
                s.Customer != null ? s.Customer.Name : null,
                s.SellerName, 
                s.SaleDate,
                s.Status,
                string.Join(", ", s.Payments.Select(p => p.PaymentMethod.ToString())),
                s.Total,
                // 👇 AGORA SIM, PASSANDO OS DADOS REAIS PARA A TELA DE NOTAS 👇
                s.NfceChave,
                s.NfceNumero,
                s.NfceUrlDanfe,
                s.NfceAmbiente,
                s.NfceStatusFocus,
                s.NfceReferencia
            ));

        // Ensina o AutoMapper a converter a entidade de Pagamento para o DTO
        CreateMap<SalePayment, SalePaymentDto>()
            .ConstructUsing(src => new SalePaymentDto(src.PaymentMethod.ToString(), src.Amount));

       CreateMap<Sale, SaleDetailDto>()
    .ConstructUsing((s, ctx) => new SaleDetailDto(
        s.Id,                                           // 1
        s.SaleNumber,                                   // 2
        s.Customer != null ? s.Customer.Name : null,    // 3
        s.SellerName,                                   // 4
        s.CustomerId,                                   // 5
        s.Customer != null ? s.Customer.Phone : null,   // 6
        s.SaleDate,                                     // 7
        s.Status,                                       // 8
        s.Subtotal,                                     // 9
        s.DiscountAmount,                               // 10
        s.Total,                                        // 11
        s.Payments.Select(p => new SalePaymentDto(p.PaymentMethod.ToString(), p.Amount)).ToList(), // 12
        ctx.Mapper.Map<List<SaleItemDto>>(s.Items),     // 13 - Coloquei a vírgula aqui!
        s.Notes                                   // 14 - 👈 A ÚLTIMA PEÇA ENCAIXA AQUI!
    ));

        CreateMap<SaleItem, SaleItemDto>()
            .ConstructUsing(i => new SaleItemDto(
                i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.DiscountPercent, i.TotalPrice)
            {
                LabelUnidadeVenda = i.Product != null ? i.Product.LabelUnidadeVenda : null,
                UnidadeEstoque    = i.Product != null ? (i.Product.UnidadeEstoque ?? i.Product.Unit) : null,
                FatorConversao    = i.Product != null ? i.Product.FatorConversao    : 1m,
            });

        CreateMap<CreateSaleItemDto, SaleItem>();
    }
}