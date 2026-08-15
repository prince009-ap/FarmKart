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
    decimal QuantityMan,
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
    decimal Amount,

    [Range(0.01, 999999999999.99, ErrorMessage = "Requested quantity must be greater than zero.")]
    decimal? RequestedQuantityKg = null
);

public sealed record AuctionBidResponse(
    Guid Id,
    Guid AuctionId,
    Guid CustomerProfileId,
    string CustomerName,
    decimal Amount,
    decimal RequestedQuantityKg,
    decimal RequestedQuantityMan,
    DateTime BidTimeUtc,
    string BidStatus,
    string? AllocationStatus = null
);

public sealed record AuctionAllocationResponse(
    Guid AllocationId,
    Guid AuctionId,
    Guid BidId,
    Guid CustomerProfileId,
    string CustomerName,
    decimal RequestedQuantityKg,
    decimal AllocatedQuantityKg,
    decimal RequestedQuantityMan,
    decimal AllocatedQuantityMan,
    decimal WinningBidAmountPerMan,
    decimal TotalPayableAmount,
    string Status,
    DateTime FinalizedAtUtc
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
    decimal QuantityMan,
    decimal RequestedQuantityKg,
    decimal RequestedQuantityMan,
    decimal CustomerBidAmount,
    decimal CurrentHighestBid,
    decimal MinimumBidIncrement,
    decimal? AllocatedQuantityKg,
    decimal? AllocatedQuantityMan,
    string AuctionStatus,
    string CustomerBidStatus,
    string? AllocationStatus,
    DateTime BidTimeUtc,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    DateTime ServerTimeUtc
);

public sealed record AuctionResultResponse(
    Guid AuctionId,
    Guid CropId,
    string CropName,
    string CropType,
    decimal Quantity,
    string Unit,
    decimal QuantityMan,
    decimal TotalAuctionQuantityKg,
    decimal TotalAllocatedQuantityKg,
    decimal RemainingQuantityKg,
    string AuctionStatus,
    bool HasWinner,
    decimal? WinningBidAmount,
    string? WinnerCustomerName,
    Guid? WinnerCustomerProfileId,
    int TotalBids,
    IReadOnlyList<AuctionAllocationResponse> Allocations,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    DateTime? FinalizedAtUtc,
    string? CustomerResultStatus,
    DateTime ServerTimeUtc
);
