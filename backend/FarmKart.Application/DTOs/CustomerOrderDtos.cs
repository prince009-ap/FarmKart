using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    string FulfillmentMode,
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
    string FulfillmentMode,
    string PaymentStatus,
    DateTime CreatedAtUtc
);

public sealed record OrderStatusHistoryResponse(
    Guid HistoryId,
    string PreviousStatus,
    string NewStatus,
    DateTime ChangedAtUtc,
    string ChangedByUserId,
    string? Note
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
    decimal RequestedQuantityKg,
    decimal RequestedQuantityMan,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal PricePerMan,
    decimal TotalAmount,
    string FarmerName,
    string? FarmLocation,
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
    DateTime AuctionStartTimeUtc,
    DateTime AuctionEndDateUtc,
    decimal AuctionQuantityKg,
    decimal AuctionQuantityMan,
    decimal WinningBidAmount,
    Guid AuctionAllocationId,
    Guid AuctionPaymentId,
    string TransactionReference,
    string PaymentMethod,
    DateTime? PaidAtUtc,
    IReadOnlyList<OrderStatusHistoryResponse> Timeline
);

public sealed record UpdateOrderStatusRequest(
    [Required] string NewStatus,
    string? Note = null
);

public sealed record UpdateFulfillmentDetailsRequest(
    [Required] string FulfillmentMode,
    string? DeliveryAddress = null,
    string? DeliveryCity = null,
    string? DeliveryState = null,
    string? DeliveryPincode = null,
    string? ContactName = null,
    string? ContactPhone = null,
    DateTime? PickupDate = null,
    DateTime? ExpectedDeliveryDate = null
);
