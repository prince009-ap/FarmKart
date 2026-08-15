using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public sealed record FarmerOrderSummaryResponse(
    int TotalOrders,
    int ConfirmedOrdersCount,
    int ReadyForPickupCount,
    int PickedUpCount,
    int DeliveredCount,
    int CompletedCount
);

public sealed record FarmerOrderFilterRequest(
    string? Search = null,
    string? Status = null,
    string? SortBy = null
);

public sealed record FarmerOrderListItemResponse(
    Guid OrderId,
    string OrderNumber,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    string? PrimaryImageUrl,
    string CustomerName,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal PricePerMan,
    decimal TotalAmount,
    string Status,
    string FulfillmentMode,
    DateTime? PickupDate,
    DateTime? ExpectedDeliveryDate,
    string PaymentStatus,
    DateTime CreatedAtUtc
);

public sealed record FarmerOrderDetailResponse(
    Guid OrderId,
    string OrderNumber,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    string? Variety,
    string? PrimaryImageUrl,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerCity,
    string? CustomerState,
    decimal RequestedQuantityKg,
    decimal RequestedQuantityMan,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal PricePerMan,
    decimal TotalAmount,
    decimal AuctionQuantityKg,
    decimal AuctionQuantityMan,
    decimal WinningBidAmountPerMan,
    DateTime AuctionStartTimeUtc,
    DateTime AuctionEndTimeUtc,
    string Status,
    string FulfillmentMode,
    string? DeliveryAddress,
    string? DeliveryCity,
    string? DeliveryState,
    string? DeliveryPincode,
    string? ContactName,
    string? ContactPhone,
    string? PickupLocation,
    DateTime? PickupDate,
    DateTime? ExpectedDeliveryDate,
    string PaymentStatus,
    DateTime OrderDateUtc,
    Guid AuctionAllocationId,
    Guid AuctionPaymentId,
    string TransactionReference,
    string PaymentMethod,
    DateTime? PaidAtUtc,
    IReadOnlyList<OrderStatusHistoryResponse> Timeline
);
