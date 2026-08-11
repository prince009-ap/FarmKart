using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;
using FarmKart.Domain.ValueObjects;

namespace FarmKart.Domain.Entities;

public sealed class Order : BaseEntity
{
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDateUtc { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;

    public ICollection<OrderItem> OrderItems { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Delivery> Deliveries { get; set; } = [];
}

public sealed class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid CropListingId { get; set; }
    public CropListing CropListing { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public sealed class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Other;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? TransactionReference { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}

public sealed class Delivery : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public DeliveryType DeliveryType { get; set; } = DeliveryType.HomeDelivery;
    public AddressInfo AddressInfo { get; set; } = new();
    public DateTime? ScheduledDateUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
}
