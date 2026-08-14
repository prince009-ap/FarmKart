using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public sealed record ProcessPaymentRequest(
    [Required] string PaymentMethod
);

public sealed record AuctionPaymentResponse(
    Guid PaymentId,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    decimal Quantity,
    string Unit,
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
    DateTime ServerTimeUtc
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
    decimal WinningBidAmount,
    decimal TotalPayableAmount,
    string Currency,
    string PaymentMethod,
    string PaymentStatus,
    string TransactionReference,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc
);
