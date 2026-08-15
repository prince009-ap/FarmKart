using System;
using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class OrderStatusHistory : BaseEntity
{
    public Guid AuctionOrderId { get; set; }
    public AuctionOrder AuctionOrder { get; set; } = null!;
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string ChangedByUserId { get; set; } = string.Empty;
    public string? Note { get; set; }
}
