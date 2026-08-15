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
