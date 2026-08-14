using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class Crop : BaseEntity
{
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;
    public string CropName { get; set; } = string.Empty;
    public string CropType { get; set; } = string.Empty;
    public string? Variety { get; set; }
    public decimal Area { get; set; }
    public FarmSizeUnit AreaUnit { get; set; } = FarmSizeUnit.Acre;
    public DateOnly? SowingDate { get; set; }
    public DateOnly? ExpectedHarvestDate { get; set; }
    public DateOnly? ActualHarvestDate { get; set; }
    public decimal Quantity { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Kilogram;
    public string? QualityGrade { get; set; }
    public string? Description { get; set; }
    public CropStatus Status { get; set; } = CropStatus.Planned;

    public ICollection<CropImage> Images { get; set; } = [];
    public ICollection<CropListing> Listings { get; set; } = [];
    public ICollection<CropStockTransaction> StockTransactions { get; set; } = [];
}

public sealed class CropListing : BaseEntity
{
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;
    public Guid CropId { get; set; }
    public Crop Crop { get; set; } = null!;
    public decimal QuantityForSale { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Kilogram;
    public decimal PricePerUnit { get; set; }
    public ListingType ListingType { get; set; } = ListingType.DirectSale;
    public ListingStatus ListingStatus { get; set; } = ListingStatus.Draft;
    public string? Description { get; set; }

    public Auction? Auction { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}

public sealed class CropImage : BaseEntity
{
    public Guid CropId { get; set; }
    public Crop Crop { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class CropStockTransaction : BaseEntity
{
    public Guid CropId { get; set; }
    public Crop Crop { get; set; } = null!;
    public decimal Quantity { get; set; }
    public MeasurementUnit Unit { get; set; } = MeasurementUnit.Kilogram;
    public decimal QuantityInBaseUnit { get; set; }
    public CropStockTransactionType TransactionType { get; set; } = CropStockTransactionType.Harvest;
    public string? Notes { get; set; }
}

