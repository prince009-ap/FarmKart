using System;
using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public sealed record ProcessPaymentRequest(
    [Required] string PaymentMethod,
    string? FulfillmentMode = null,
    string? DeliveryAddress = null,
    string? DeliveryCity = null,
    string? DeliveryState = null,
    string? DeliveryPincode = null,
    string? ContactName = null,
    string? ContactPhone = null,
    DateTime? PickupDate = null
);

public sealed record AuctionPaymentResponse(
    Guid PaymentId,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    decimal Quantity,
    string Unit,
    decimal QuantityMan,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal WinningBidAmount,
    decimal TotalPayableAmount,
    string Currency,
    string PaymentMethod,
    string PaymentStatus,
    string TransactionReference,
    string WinnerCustomerName,
    string FarmerName,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime ServerTimeUtc,
    AuctionOrderResponse? Order = null
);

public sealed record CustomerPaymentHistoryResponse(
    Guid PaymentId,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string? PrimaryImageUrl,
    string CropType,
    decimal Quantity,
    string Unit,
    decimal QuantityMan,
    decimal AllocatedQuantityKg,
    decimal AllocatedQuantityMan,
    decimal WinningBidAmount,
    decimal TotalPayableAmount,
    string Currency,
    string PaymentMethod,
    string PaymentStatus,
    string TransactionReference,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc
);
