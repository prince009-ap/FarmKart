using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FarmKart.Application.DTOs;

public sealed record CreateFarmerAuctionRequest(
    Guid CropId,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal Quantity,
    [Required, StringLength(20)] string Unit,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal StartingBidPrice,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal MinimumBidIncrement,
    DateTime StartTimeUtc,
    [Required] string Duration,
    [StringLength(1000)] string? Description);

public sealed record UpdateFarmerAuctionRequest(
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal Quantity,
    [Required, StringLength(20)] string Unit,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal StartingBidPrice,
    [Range(typeof(decimal), "0.01", "9999999999999999")] decimal MinimumBidIncrement,
    DateTime StartTimeUtc,
    [Required] string Duration,
    [StringLength(1000)] string? Description);

public sealed record FarmerAuctionPaymentSummary(
    decimal TotalWinningAmount,
    decimal PaidAmount,
    decimal PendingAmount,
    int TotalPaidCount,
    int TotalPendingCount);

public sealed record FarmerAuctionResponse(
    Guid Id,
    Guid CropId,
    string CropName,
    string? Variety,
    string? PrimaryImageUrl,
    decimal Quantity,
    string Unit,
    decimal QuantityKg,
    decimal QuantityMan,
    decimal AvailableStockKg,
    decimal ReservedStockKg,
    decimal RemainingUnreservedStockKg,
    decimal StartingBidPrice,
    decimal MinimumBidIncrement,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    string Status,
    string? Description,
    int TotalBids,
    decimal CurrentHighestBid,
    decimal TotalRequestedQuantityKg,
    decimal TotalRequestedQuantityMan,
    decimal DemandPercentage,
    decimal TotalAllocatedQuantityKg,
    decimal TotalAllocatedQuantityMan,
    decimal RemainingQuantityKg,
    int WinnersCount,
    decimal? WinningBidAmount,
    FarmerAuctionPaymentSummary? PaymentSummary,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime ServerTimeUtc);

public sealed record FarmerAuctionBidResponse(
    Guid BidId,
    Guid AuctionId,
    Guid CustomerProfileId,
    string CustomerName,
    decimal RequestedQuantityKg,
    decimal RequestedQuantityMan,
    decimal BidAmountPerMan,
    DateTime BidTimeUtc,
    string BidStatus,
    string? AllocationStatus);

public sealed record FarmerAuctionSummaryCountsResponse(
    int TotalAuctions,
    int UpcomingCount,
    int LiveCount,
    int EndedCount,
    int CancelledCount);
