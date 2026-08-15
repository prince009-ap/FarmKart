using FarmKart.Domain.Common;

namespace FarmKart.Domain.Entities;

public sealed class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid AuctionOrderId { get; set; }
    public AuctionOrder AuctionOrder { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;

    public DateTime InvoiceDateUtc { get; set; } = DateTime.UtcNow;

    // Snapshot details
    public string SellerName { get; set; } = string.Empty;
    public string? SellerPhone { get; set; }
    public string? SellerLocation { get; set; }

    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerPhone { get; set; }
    public string? DeliveryOrPickupAddress { get; set; }

    public string CropName { get; set; } = string.Empty;
    public string CropType { get; set; } = string.Empty;
    public string Variety { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }

    public decimal QuantityKg { get; set; }
    public decimal QuantityMan { get; set; }
    public decimal PricePerMan { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal TaxAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; }

    public string PaymentStatus { get; set; } = "PAID";
    public string PaymentReference { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }

    public string FulfillmentMode { get; set; } = string.Empty;
}
