using System.Data;
using FarmKart.Application.Abstractions.Auctions;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class AuctionFinalizationService(FarmKartDbContext dbContext) : IAuctionFinalizationService
{
    public async Task<int> FinalizeExpiredAuctionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredAuctions = await dbContext.Auctions
            .Include(a => a.Allocations)
            .Include(a => a.Bids)
            .Where(a => a.EndTimeUtc <= now && a.AuctionStatus != AuctionStatus.Cancelled && a.AuctionStatus != AuctionStatus.Draft)
            .ToListAsync(cancellationToken);

        int finalizedCount = 0;

        foreach (var auction in expiredAuctions)
        {
            var finalized = await FinalizeSingleAuctionInternalAsync(auction.Id, now, cancellationToken);
            if (finalized)
            {
                finalizedCount++;
            }
        }

        return finalizedCount;
    }

    public async Task<AuctionResultResponse> GetAuctionResultAsync(
        Guid auctionId,
        Guid? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.AuctionWinner)
                .ThenInclude(w => w.CustomerProfile)
            .Include(a => a.Allocations)
                .ThenInclude(al => al.CustomerProfile)
            .Include(a => a.Bids)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction == null)
        {
            throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");
        }

        if (now >= auction.EndTimeUtc && auction.Allocations.Count == 0)
        {
            await FinalizeSingleAuctionInternalAsync(auctionId, now, cancellationToken);

            auction = await dbContext.Auctions
                .AsNoTracking()
                .Include(a => a.AuctionWinner)
                    .ThenInclude(w => w.CustomerProfile)
                .Include(a => a.Allocations)
                    .ThenInclude(al => al.CustomerProfile)
                .Include(a => a.Bids)
                .Include(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
                .FirstAsync(a => a.Id == auctionId, cancellationToken);
        }

        var crop = auction.CropListing.Crop;
        var totalBids = auction.Bids.Count;
        var totalAuctionKg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
        var totalAuctionMan = AuctionPricingConstants.ConvertKgToMan(totalAuctionKg);

        var allocationsList = auction.Allocations
            .OrderByDescending(al => al.WinningBidAmountPerMan)
            .ThenBy(al => al.FinalizedAtUtc)
            .ToList();

        var totalAllocatedKg = allocationsList.Sum(al => al.AllocatedQuantityKg);
        var remainingKg = Math.Max(0m, totalAuctionKg - totalAllocatedKg);
        var hasWinner = allocationsList.Any(al => al.AllocatedQuantityKg > 0);

        var allocationDtos = allocationsList.Select(al =>
        {
            var reqMan = AuctionPricingConstants.ConvertKgToMan(al.RequestedQuantityKg);
            var allocMan = AuctionPricingConstants.ConvertKgToMan(al.AllocatedQuantityKg);
            var totalPayable = Math.Round(allocMan * al.WinningBidAmountPerMan, 2);

            return new AuctionAllocationResponse(
                AllocationId: al.Id,
                AuctionId: al.AuctionId,
                BidId: al.BidId,
                CustomerProfileId: al.CustomerProfileId,
                CustomerName: al.CustomerProfile?.FullName ?? "Customer",
                RequestedQuantityKg: al.RequestedQuantityKg,
                AllocatedQuantityKg: al.AllocatedQuantityKg,
                RequestedQuantityMan: reqMan,
                AllocatedQuantityMan: allocMan,
                WinningBidAmountPerMan: al.WinningBidAmountPerMan,
                TotalPayableAmount: totalPayable,
                Status: al.Status switch
                {
                    AllocationStatus.Won => "WON",
                    AllocationStatus.PartiallyWon => "PARTIALLY_WON",
                    _ => "LOST"
                },
                FinalizedAtUtc: al.FinalizedAtUtc
            );
        }).ToList();

        string? customerResultStatus = null;
        if (requestingUserId.HasValue)
        {
            var customerProfile = await dbContext.CustomerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == requestingUserId.Value, cancellationToken);

            if (customerProfile != null)
            {
                var custAllocation = allocationsList.FirstOrDefault(al => al.CustomerProfileId == customerProfile.Id);
                if (custAllocation != null)
                {
                    customerResultStatus = custAllocation.Status switch
                    {
                        AllocationStatus.Won => "WON",
                        AllocationStatus.PartiallyWon => "PARTIALLY_WON",
                        _ => "LOST"
                    };
                }
                else if (totalBids == 0)
                {
                    customerResultStatus = "NO WINNER";
                }
                else
                {
                    customerResultStatus = "DID NOT BID";
                }
            }
        }

        string effectiveStatus = now < auction.StartTimeUtc ? "UPCOMING" : (now <= auction.EndTimeUtc ? "LIVE" : "ENDED");

        var topAllocation = allocationsList.FirstOrDefault(al => al.AllocatedQuantityKg > 0);

        return new AuctionResultResponse(
            AuctionId: auction.Id,
            CropId: crop.Id,
            CropName: crop.CropName,
            CropType: crop.CropType,
            Quantity: auction.CropListing.QuantityForSale,
            Unit: CropStockUnitConverter.Format(auction.CropListing.Unit),
            QuantityMan: totalAuctionMan,
            TotalAuctionQuantityKg: totalAuctionKg,
            TotalAllocatedQuantityKg: totalAllocatedKg,
            RemainingQuantityKg: remainingKg,
            AuctionStatus: effectiveStatus,
            HasWinner: hasWinner,
            WinningBidAmount: topAllocation?.WinningBidAmountPerMan ?? auction.AuctionWinner?.FinalAmount,
            WinnerCustomerName: topAllocation?.CustomerProfile?.FullName ?? auction.AuctionWinner?.CustomerProfile?.FullName,
            WinnerCustomerProfileId: topAllocation?.CustomerProfileId ?? auction.AuctionWinner?.CustomerProfileId,
            TotalBids: totalBids,
            Allocations: allocationDtos,
            StartTimeUtc: auction.StartTimeUtc,
            EndTimeUtc: auction.EndTimeUtc,
            FinalizedAtUtc: allocationsList.FirstOrDefault()?.FinalizedAtUtc ?? auction.AuctionWinner?.SelectedAtUtc,
            CustomerResultStatus: customerResultStatus,
            ServerTimeUtc: now
        );
    }

    private async Task<bool> FinalizeSingleAuctionInternalAsync(Guid auctionId, DateTime now, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var auction = await dbContext.Auctions
                .Include(a => a.AuctionWinner)
                .Include(a => a.Allocations)
                .Include(a => a.Bids)
                .Include(a => a.CropListing)
                .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

            if (auction == null)
            {
                return false;
            }

            if (auction.AuctionStatus != AuctionStatus.Cancelled && auction.AuctionStatus != AuctionStatus.Draft)
            {
                auction.AuctionStatus = AuctionStatus.Ended;
            }

            if (auction.Allocations.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var totalAuctionKg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
            var activeBids = auction.Bids
                .Where(b => b.BidStatus == BidStatus.Active || b.BidStatus == BidStatus.Winning)
                .OrderByDescending(b => b.Amount)
                .ThenBy(b => b.BidTimeUtc)
                .ToList();

            decimal remainingKg = totalAuctionKg;

            foreach (var bid in activeBids)
            {
                var requestedKg = bid.RequestedQuantityKg > 0 ? bid.RequestedQuantityKg : totalAuctionKg;
                decimal allocatedKg;
                AllocationStatus status;

                if (remainingKg >= requestedKg)
                {
                    allocatedKg = requestedKg;
                    status = (allocatedKg >= requestedKg) ? AllocationStatus.Won : AllocationStatus.PartiallyWon;
                    remainingKg -= allocatedKg;
                    bid.BidStatus = BidStatus.Winning;
                }
                else if (remainingKg > 0)
                {
                    allocatedKg = remainingKg;
                    status = AllocationStatus.PartiallyWon;
                    remainingKg = 0;
                    bid.BidStatus = BidStatus.Winning;
                }
                else
                {
                    allocatedKg = 0;
                    status = AllocationStatus.Lost;
                    bid.BidStatus = BidStatus.Outbid;
                }

                var allocation = new AuctionAllocation
                {
                    AuctionId = auction.Id,
                    BidId = bid.Id,
                    CustomerProfileId = bid.CustomerProfileId,
                    RequestedQuantityKg = requestedKg,
                    AllocatedQuantityKg = allocatedKg,
                    WinningBidAmountPerMan = bid.Amount,
                    Status = status,
                    FinalizedAtUtc = now
                };

                dbContext.AuctionAllocations.Add(allocation);
            }

            // Maintain legacy primary winner reference if allocations exist
            var primaryWinningAllocation = dbContext.AuctionAllocations.Local
                .Where(a => a.AuctionId == auction.Id && a.AllocatedQuantityKg > 0)
                .OrderByDescending(a => a.WinningBidAmountPerMan)
                .ThenBy(a => a.FinalizedAtUtc)
                .FirstOrDefault();

            if (primaryWinningAllocation != null && auction.AuctionWinner == null)
            {
                var winner = new AuctionWinner
                {
                    AuctionId = auction.Id,
                    CustomerProfileId = primaryWinningAllocation.CustomerProfileId,
                    WinningBidId = primaryWinningAllocation.BidId,
                    FinalAmount = primaryWinningAllocation.WinningBidAmountPerMan,
                    SelectedAtUtc = now
                };
                dbContext.AuctionWinners.Add(winner);
                auction.CurrentHighestBid = primaryWinningAllocation.WinningBidAmountPerMan;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        });
    }
}
