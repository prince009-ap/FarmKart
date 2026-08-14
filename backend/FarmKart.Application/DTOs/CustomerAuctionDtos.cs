using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public sealed record CustomerAuctionResponse(
    Guid Id,
    Guid CropId,
    string CropName,
    string CropType,
    string? Variety,
    decimal Quantity,
    string Unit,
    decimal QuantityKg,
    decimal StartingBidPrice,
    decimal CurrentHighestBid,
    decimal MinimumBidIncrement,
    string FarmerName,
    string FarmLocation,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    string Status,
    string? PrimaryImageUrl,
    IReadOnlyList<string> Images,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime ServerTimeUtc
);

public sealed record CustomerAuctionFilterRequest(
    string? Search = null,
    string? Category = null,
    string? Status = null,
    string? Location = null,
    string? SortBy = null
);

public sealed record PlaceBidRequest(
    [Range(0.01, 999999999999.99, ErrorMessage = "Bid amount must be greater than zero.")]
    decimal Amount
);

public sealed record AuctionBidResponse(
    Guid Id,
    Guid AuctionId,
    Guid CustomerProfileId,
    string CustomerName,
    decimal Amount,
    DateTime BidTimeUtc,
    string BidStatus
);

public sealed record CustomerMyBidResponse(
    Guid BidId,
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string? PrimaryImageUrl,
    string CropType,
    decimal Quantity,
    string Unit,
    decimal CustomerBidAmount,
    decimal CurrentHighestBid,
    decimal MinimumBidIncrement,
    string AuctionStatus,
    string CustomerBidStatus,
    DateTime BidTimeUtc,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    DateTime ServerTimeUtc
);
