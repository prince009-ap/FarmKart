using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerAuctionService(FarmKartDbContext dbContext) : IFarmerAuctionService
{
    private static readonly AuctionStatus[] ReservingStatuses = [AuctionStatus.Draft, AuctionStatus.Scheduled, AuctionStatus.Live];

    public static double ParseDurationToHours(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            throw new ArgumentException("Duration is required.");
        }

        var normalized = duration.Trim();
        if (normalized.Equals("5 Hours", StringComparison.OrdinalIgnoreCase)) return 5;
        if (normalized.Equals("12 Hours", StringComparison.OrdinalIgnoreCase)) return 12;
        if (normalized.Equals("1 Day", StringComparison.OrdinalIgnoreCase)) return 24;
        if (normalized.Equals("3 Days", StringComparison.OrdinalIgnoreCase)) return 72;
        if (normalized.Equals("7 Days", StringComparison.OrdinalIgnoreCase)) return 168;

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && double.TryParse(parts[0], out var val))
        {
            if (val <= 0)
            {
                throw new ArgumentException("Duration must be greater than zero.");
            }

            if (parts.Length > 1)
            {
                var unit = parts[1].ToLower();
                if (unit.StartsWith("hour"))
                {
                    return val;
                }
                if (unit.StartsWith("day"))
                {
                    return val * 24;
                }
                throw new ArgumentException("Invalid duration unit. Supported units are Hours and Days.");
            }
            return val;
        }

        throw new ArgumentException("Invalid duration option.");
    }

    public static AuctionStatus GetEffectiveStatus(Auction auction, DateTime now)
    {
        if (auction.AuctionStatus == AuctionStatus.Cancelled) return AuctionStatus.Cancelled;
        if (auction.AuctionStatus == AuctionStatus.Draft) return AuctionStatus.Draft;
        if (now < auction.StartTimeUtc) return AuctionStatus.Scheduled;
        if (now <= auction.EndTimeUtc) return AuctionStatus.Live;
        return AuctionStatus.Ended;
    }

    public async Task<IReadOnlyList<FarmerAuctionResponse>> GetAuctionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerAsync(userId, cancellationToken);
        var auctions = await dbContext.Auctions
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.Images)
            .Include(a => a.Bids)
            .Include(a => a.Allocations)
            .Include(a => a.AuctionPayments)
            .Where(a => a.FarmerProfileId == farmer.Id)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var result = new List<FarmerAuctionResponse>();
        foreach (var auction in auctions)
        {
            result.Add(await MapAsync(auction, cancellationToken));
        }
        return result;
    }

    public async Task<FarmerAuctionResponse> GetAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default)
    {
        var auction = await FindOwnedAsync(userId, auctionId, cancellationToken);
        return await MapAsync(auction, cancellationToken);
    }

    public async Task<IReadOnlyList<FarmerAuctionBidResponse>> GetAuctionBidsAsync(
        Guid userId,
        Guid auctionId,
        string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        var auction = await FindOwnedAsync(userId, auctionId, cancellationToken);

        var totalAuctionKg = auction.CropListing != null
            ? CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit)
            : 0m;

        var query = dbContext.Bids
            .Include(b => b.CustomerProfile)
            .Where(b => b.AuctionId == auction.Id);

        var sortedBids = sortBy?.ToLowerInvariant() switch
        {
            "lowest_bid" => query.OrderBy(b => b.Amount).ThenBy(b => b.BidTimeUtc),
            "highest_qty" => query.OrderByDescending(b => b.RequestedQuantityKg > 0 ? b.RequestedQuantityKg : totalAuctionKg).ThenByDescending(b => b.Amount),
            "latest_bid" => query.OrderByDescending(b => b.BidTimeUtc),
            "earliest_bid" => query.OrderBy(b => b.BidTimeUtc),
            _ => query.OrderByDescending(b => b.Amount).ThenBy(b => b.BidTimeUtc)
        };

        var bids = await sortedBids.ToListAsync(cancellationToken);
        var allocations = (auction.Allocations ?? []).ToDictionary(a => a.BidId);

        return bids.Select(b =>
        {
            var reqKg = b.RequestedQuantityKg > 0 ? b.RequestedQuantityKg : totalAuctionKg;
            var reqMan = AuctionPricingConstants.ConvertKgToMan(reqKg);
            string? allocStatus = allocations.TryGetValue(b.Id, out var alloc)
                ? (alloc.Status switch
                {
                    AllocationStatus.Won => "WON",
                    AllocationStatus.PartiallyWon => "PARTIALLY_WON",
                    _ => "LOST"
                })
                : null;

            return new FarmerAuctionBidResponse(
                BidId: b.Id,
                AuctionId: b.AuctionId,
                CustomerProfileId: b.CustomerProfileId,
                CustomerName: b.CustomerProfile?.FullName ?? "Customer",
                RequestedQuantityKg: reqKg,
                RequestedQuantityMan: reqMan,
                BidAmountPerMan: b.Amount,
                BidTimeUtc: b.BidTimeUtc,
                BidStatus: b.BidStatus.ToString(),
                AllocationStatus: allocStatus
            );
        }).ToList();
    }

    public async Task<FarmerAuctionSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;

        var auctions = await dbContext.Auctions
            .Where(a => a.FarmerProfileId == farmer.Id)
            .Select(a => new { a.AuctionStatus, a.StartTimeUtc, a.EndTimeUtc })
            .ToListAsync(cancellationToken);

        int total = auctions.Count;
        int upcoming = 0;
        int live = 0;
        int ended = 0;
        int cancelled = 0;

        foreach (var a in auctions)
        {
            if (a.AuctionStatus == AuctionStatus.Cancelled)
            {
                cancelled++;
            }
            else if (a.AuctionStatus == AuctionStatus.Draft || now < a.StartTimeUtc)
            {
                upcoming++;
            }
            else if (now <= a.EndTimeUtc)
            {
                live++;
            }
            else
            {
                ended++;
            }
        }

        return new FarmerAuctionSummaryCountsResponse(
            TotalAuctions: total,
            UpcomingCount: upcoming,
            LiveCount: live,
            EndedCount: ended,
            CancelledCount: cancelled
        );
    }

    public async Task<FarmerAuctionResponse> CreateAuctionAsync(Guid userId, CreateFarmerAuctionRequest request, CancellationToken cancellationToken = default)
    {
        var farmer = await GetFarmerAsync(userId, cancellationToken);
        var hours = ParseDurationToHours(request.Duration);
        var endTimeUtc = request.StartTimeUtc.AddHours(hours);
        ValidateSchedule(request.StartTimeUtc, endTimeUtc);
        var unit = CropStockUnitConverter.Parse(request.Unit);
        var crop = await dbContext.Crops.Include(c => c.StockTransactions)
            .FirstOrDefaultAsync(c => c.Id == request.CropId && c.FarmerProfileId == farmer.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Crop not found for authenticated farmer.");

        await EnsureAvailableAsync(crop, request.Quantity, unit, null, cancellationToken);
        var status = request.StartTimeUtc <= DateTime.UtcNow ? AuctionStatus.Live : AuctionStatus.Scheduled;
        var listing = new CropListing { CropId = crop.Id, FarmerProfileId = farmer.Id, QuantityForSale = request.Quantity, Unit = unit, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active, Description = request.Description?.Trim() };
        var auction = new Auction { CropListing = listing, FarmerProfileId = farmer.Id, StartingPrice = request.StartingBidPrice, CurrentHighestBid = 0m, MinimumBidIncrement = request.MinimumBidIncrement, StartTimeUtc = request.StartTimeUtc, EndTimeUtc = endTimeUtc, AuctionStatus = status };
        dbContext.Auctions.Add(auction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(auction, cancellationToken);
    }

    public async Task<FarmerAuctionResponse> UpdateAuctionAsync(Guid userId, Guid auctionId, UpdateFarmerAuctionRequest request, CancellationToken cancellationToken = default)
    {
        var auction = await FindOwnedAsync(userId, auctionId, cancellationToken);
        if (auction.AuctionStatus is not (AuctionStatus.Draft or AuctionStatus.Scheduled)) throw new InvalidOperationException("Only draft or scheduled auctions can be edited.");
        var hours = ParseDurationToHours(request.Duration);
        var endTimeUtc = request.StartTimeUtc.AddHours(hours);
        ValidateSchedule(request.StartTimeUtc, endTimeUtc);
        var unit = CropStockUnitConverter.Parse(request.Unit);
        await EnsureAvailableAsync(auction.CropListing.Crop, request.Quantity, unit, auction.Id, cancellationToken);
        auction.CropListing.QuantityForSale = request.Quantity; auction.CropListing.Unit = unit; auction.CropListing.Description = request.Description?.Trim();
        auction.StartingPrice = request.StartingBidPrice; auction.MinimumBidIncrement = request.MinimumBidIncrement; auction.StartTimeUtc = request.StartTimeUtc; auction.EndTimeUtc = endTimeUtc;
        auction.AuctionStatus = request.StartTimeUtc <= DateTime.UtcNow ? AuctionStatus.Live : AuctionStatus.Scheduled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapAsync(auction, cancellationToken);
    }

    public async Task CancelAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default)
    {
        var auction = await FindOwnedAsync(userId, auctionId, cancellationToken);
        if (auction.AuctionStatus is AuctionStatus.Ended or AuctionStatus.Cancelled) throw new InvalidOperationException("This auction cannot be cancelled.");
        auction.AuctionStatus = AuctionStatus.Cancelled; auction.CropListing.ListingStatus = ListingStatus.Closed;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAvailableAsync(Crop crop, decimal quantity, MeasurementUnit unit, Guid? excludedAuctionId, CancellationToken ct)
    {
        if (quantity <= 0) throw new ArgumentException("Auction quantity must be greater than zero.");
        var totalKg = crop.StockTransactions.Sum(t => t.QuantityInBaseUnit);
        var now = DateTime.UtcNow;
        var reservedKg = await dbContext.Auctions.Include(a => a.CropListing)
            .Where(a => a.CropListing.CropId == crop.Id && ReservingStatuses.Contains(a.AuctionStatus) && (a.AuctionStatus == AuctionStatus.Draft || a.EndTimeUtc > now) && (!excludedAuctionId.HasValue || a.Id != excludedAuctionId.Value))
            .SumAsync(a => (decimal?)a.CropListing.QuantityForSale * (a.CropListing.Unit == MeasurementUnit.Ton ? 1000m : a.CropListing.Unit == MeasurementUnit.Quintal ? 100m : 1m), ct) ?? 0m;
        if (CropStockUnitConverter.ToKilograms(quantity, unit) + reservedKg > totalKg) throw new ArgumentException("Auction quantity exceeds available unreserved stock.");
    }

    private static void ValidateSchedule(DateTime start, DateTime end)
    {
        if (start < DateTime.UtcNow.AddMinutes(-5)) throw new ArgumentException("Auction start time must be now or in the future.");
        if (end <= start) throw new ArgumentException("Auction end time must be after start time.");
    }

    private async Task<FarmerProfile> GetFarmerAsync(Guid userId, CancellationToken ct) =>
        await dbContext.FarmerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, ct)
        ?? throw new KeyNotFoundException("Farmer profile not found.");

    private async Task<Auction> FindOwnedAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var farmer = await GetFarmerAsync(userId, ct);
        return await dbContext.Auctions
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.StockTransactions)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.Images)
            .Include(a => a.Bids)
            .Include(a => a.Allocations)
            .Include(a => a.AuctionPayments)
            .FirstOrDefaultAsync(a => a.Id == id && a.FarmerProfileId == farmer.Id, ct)
            ?? throw new KeyNotFoundException("Auction not found.");
    }

    private async Task<FarmerAuctionResponse> MapAsync(Auction auction, CancellationToken ct)
    {
        var cropListing = auction.CropListing;
        var crop = cropListing?.Crop;
        var cropId = crop?.Id ?? cropListing?.CropId ?? Guid.Empty;
        var cropName = crop?.CropName ?? "Crop";
        var variety = crop?.Variety;
        var primaryImage = crop?.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? crop?.Images?.FirstOrDefault()?.ImageUrl;

        decimal harvestBase = cropId != Guid.Empty
            ? await dbContext.CropStockTransactions.Where(t => t.CropId == cropId && t.TransactionType == CropStockTransactionType.Harvest).SumAsync(t => (decimal?)t.QuantityInBaseUnit, ct) ?? 0m
            : 0m;

        if (harvestBase == 0m && cropListing?.Crop != null)
        {
            harvestBase = cropListing.Crop.Quantity;
        }

        decimal adjustments = cropId != Guid.Empty
            ? await dbContext.CropStockTransactions.Where(t => t.CropId == cropId && t.TransactionType != CropStockTransactionType.Harvest).SumAsync(t => (decimal?)t.QuantityInBaseUnit, ct) ?? 0m
            : 0m;

        var totalStock = Math.Max(0m, harvestBase + adjustments);

        var now = DateTime.UtcNow;
        var reserved = cropId != Guid.Empty
            ? await dbContext.Auctions.Include(a => a.CropListing).Where(a => a.CropListing.CropId == cropId && ReservingStatuses.Contains(a.AuctionStatus) && (a.AuctionStatus == AuctionStatus.Draft || a.EndTimeUtc > now)).SumAsync(a => (decimal?)a.CropListing.QuantityForSale * (a.CropListing.Unit == MeasurementUnit.Ton ? 1000m : a.CropListing.Unit == MeasurementUnit.Quintal ? 100m : 1m), ct) ?? 0m
            : 0m;

        var quantityForSale = cropListing?.QuantityForSale ?? 0m;
        var unitEnum = cropListing?.Unit ?? MeasurementUnit.Kilogram;
        var kg = CropStockUnitConverter.ToKilograms(quantityForSale, unitEnum);
        var man = AuctionPricingConstants.ConvertKgToMan(kg);
        var effectiveStatusEnum = GetEffectiveStatus(auction, now);
        var status = effectiveStatusEnum.ToString();

        var activeBids = (auction.Bids ?? []).Where(b => b.BidStatus != BidStatus.Cancelled).ToList();
        var totalBidsCount = activeBids.Count;
        var currentHighestBid = activeBids.Count > 0 ? activeBids.Max(b => b.Amount) : auction.StartingPrice;

        var totalRequestedKg = activeBids.Sum(b => b.RequestedQuantityKg > 0 ? b.RequestedQuantityKg : kg);
        var totalRequestedMan = AuctionPricingConstants.ConvertKgToMan(totalRequestedKg);
        var demandPct = kg > 0 ? Math.Round((totalRequestedKg / kg) * 100m, 2) : 0m;

        var allocations = (auction.Allocations ?? []).ToList();
        var totalAllocatedKg = allocations.Sum(al => al.AllocatedQuantityKg);
        var totalAllocatedMan = AuctionPricingConstants.ConvertKgToMan(totalAllocatedKg);
        var remainingKg = Math.Max(0m, kg - totalAllocatedKg);

        var winningAllocations = allocations
            .Where(al => (al.Status == AllocationStatus.Won || al.Status == AllocationStatus.PartiallyWon) && al.AllocatedQuantityKg > 0)
            .ToList();

        var winningCustomerIds = winningAllocations.Select(al => al.CustomerProfileId).Distinct().ToList();
        var winnersCount = winningCustomerIds.Count;
        decimal? winningBidAmount = winningAllocations.Count > 0 ? winningAllocations.Max(al => al.WinningBidAmountPerMan) : null;

        FarmerAuctionPaymentSummary? paymentSummary = null;
        if (effectiveStatusEnum == AuctionStatus.Ended && winnersCount > 0)
        {
            var payments = (auction.AuctionPayments ?? []).ToList();
            var paidCustomerIds = payments
                .Where(p => p.PaymentStatus == PaymentStatus.Paid)
                .Select(p => p.CustomerProfileId)
                .Distinct()
                .Where(id => winningCustomerIds.Contains(id))
                .ToList();

            decimal totalWinningAmt = winningAllocations.Sum(al => Math.Round(AuctionPricingConstants.ConvertKgToMan(al.AllocatedQuantityKg) * al.WinningBidAmountPerMan, 2));
            decimal paidAmt = payments.Where(p => p.PaymentStatus == PaymentStatus.Paid).Sum(p => p.Amount);
            decimal pendingAmt = Math.Max(0m, totalWinningAmt - paidAmt);
            int paidCount = paidCustomerIds.Count;
            int pendingCount = Math.Max(0, winnersCount - paidCount);

            paymentSummary = new FarmerAuctionPaymentSummary(
                TotalWinningAmount: totalWinningAmt,
                PaidAmount: paidAmt,
                PendingAmount: pendingAmt,
                TotalPaidCount: paidCount,
                TotalPendingCount: pendingCount
            );
        }

        return new FarmerAuctionResponse(
            Id: auction.Id,
            CropId: cropId,
            CropName: cropName,
            Variety: variety,
            PrimaryImageUrl: primaryImage,
            Quantity: quantityForSale,
            Unit: CropStockUnitConverter.Format(unitEnum),
            QuantityKg: kg,
            QuantityMan: man,
            AvailableStockKg: totalStock,
            ReservedStockKg: reserved,
            RemainingUnreservedStockKg: Math.Max(0m, totalStock - reserved),
            StartingBidPrice: auction.StartingPrice,
            MinimumBidIncrement: auction.MinimumBidIncrement,
            StartTimeUtc: auction.StartTimeUtc,
            EndTimeUtc: auction.EndTimeUtc,
            Status: status,
            Description: cropListing?.Description,
            TotalBids: totalBidsCount,
            CurrentHighestBid: currentHighestBid,
            TotalRequestedQuantityKg: totalRequestedKg,
            TotalRequestedQuantityMan: totalRequestedMan,
            DemandPercentage: demandPct,
            TotalAllocatedQuantityKg: totalAllocatedKg,
            TotalAllocatedQuantityMan: totalAllocatedMan,
            RemainingQuantityKg: remainingKg,
            WinnersCount: winnersCount,
            WinningBidAmount: winningBidAmount,
            PaymentSummary: paymentSummary,
            CreatedAtUtc: auction.CreatedAtUtc,
            UpdatedAtUtc: auction.UpdatedAtUtc,
            ServerTimeUtc: now
        );
    }
}
