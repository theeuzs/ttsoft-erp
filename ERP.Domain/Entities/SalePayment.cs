using ERP.Domain.Common; // 👈 Adicionado para achar o BaseEntity
using ERP.Domain.Enums;
using System;

namespace ERP.Domain.Entities;

// 👇 Trocado de Entity para BaseEntity
public class SalePayment : BaseEntity 
{
    public Guid SaleId { get; set; }
    public virtual Sale Sale { get; set; } = null!;
    
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
}