namespace FarmKart.Application.DTOs;

public sealed record AuctionOrderResponse(
    Guid OrderId,
    string OrderNumber,
    Guid AuctionId,
    Guid AuctionPaymentId,
    Guid AuctionAllocationId,
    string CropName,
    string CropType,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal PricePerMan,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc
);

public sealed record CustomerOrderFilterRequest(
    string? Search = null,
    string? Status = null,
    string? SortBy = null
);

public sealed record CustomerOrderListItemResponse(
    Guid OrderId,
    string OrderNumber,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    string? PrimaryImageUrl,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal PricePerMan,
    decimal TotalAmount,
    string FarmerName,
    string Status,
    string PaymentStatus,
    DateTime CreatedAtUtc
);

public sealed record CustomerOrderDetailResponse(
    Guid OrderId,
    string OrderNumber,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    string? Variety,
    string? PrimaryImageUrl,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal PricePerMan,
    decimal TotalAmount,
    string FarmerName,
    string? FarmLocation,
    string Status,
    string PaymentStatus,
    DateTime OrderDateUtc,
    DateTime AuctionEndDateUtc,
    decimal WinningBidAmount,
    Guid AuctionAllocationId,
    Guid AuctionPaymentId,
    string TransactionReference,
    string PaymentMethod
);

