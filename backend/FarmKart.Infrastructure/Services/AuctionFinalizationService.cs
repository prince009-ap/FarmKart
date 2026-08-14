using System.Data;
using FarmKart.Application.Abstractions.Auctions;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
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
            .Include(a => a.AuctionWinner)
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
            .Include(a => a.AuctionWinner)
                .ThenInclude(w => w.WinningBid)
            .Include(a => a.Bids)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction == null)
        {
            throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");
        }

        // On-demand finalization check if auction has expired but winner not finalized
        if (now >= auction.EndTimeUtc && auction.AuctionWinner == null)
        {
            await FinalizeSingleAuctionInternalAsync(auctionId, now, cancellationToken);

            // Re-fetch updated auction
            auction = await dbContext.Auctions
                .AsNoTracking()
                .Include(a => a.AuctionWinner)
                    .ThenInclude(w => w.CustomerProfile)
                .Include(a => a.AuctionWinner)
                    .ThenInclude(w => w.WinningBid)
                .Include(a => a.Bids)
                .Include(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
                .FirstAsync(a => a.Id == auctionId, cancellationToken);
        }

        var crop = auction.CropListing.Crop;
        var activeBids = auction.Bids.Where(b => b.BidStatus == BidStatus.Active).ToList();
        var totalBids = activeBids.Count;
        var hasWinner = auction.AuctionWinner != null;

        string? customerResultStatus = null;
        if (requestingUserId.HasValue)
        {
            var customerProfile = await dbContext.CustomerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == requestingUserId.Value, cancellationToken);

            if (customerProfile != null)
            {
                if (hasWinner && auction.AuctionWinner!.CustomerProfileId == customerProfile.Id)
                {
                    customerResultStatus = "WON";
                }
                else if (activeBids.Any(b => b.CustomerProfileId == customerProfile.Id))
                {
                    customerResultStatus = "LOST";
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

        return new AuctionResultResponse(
            AuctionId: auction.Id,
            CropId: crop.Id,
            CropName: crop.CropName,
            CropType: crop.CropType,
            Quantity: auction.CropListing.QuantityForSale,
            Unit: CropStockUnitConverter.Format(auction.CropListing.Unit),
            AuctionStatus: effectiveStatus,
            HasWinner: hasWinner,
            WinningBidAmount: auction.AuctionWinner?.FinalAmount,
            WinnerCustomerName: auction.AuctionWinner?.CustomerProfile?.FullName,
            WinnerCustomerProfileId: auction.AuctionWinner?.CustomerProfileId,
            TotalBids: totalBids,
            StartTimeUtc: auction.StartTimeUtc,
            EndTimeUtc: auction.EndTimeUtc,
            FinalizedAtUtc: auction.AuctionWinner?.SelectedAtUtc,
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
                .Include(a => a.Bids)
                .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

            if (auction == null)
            {
                return false;
            }

            // Always ensure status is set to Ended if EndTimeUtc has passed
            if (auction.AuctionStatus != AuctionStatus.Cancelled && auction.AuctionStatus != AuctionStatus.Draft)
            {
                auction.AuctionStatus = AuctionStatus.Ended;
            }

            // Idempotency check: if winner already finalized, skip duplicate creation
            if (auction.AuctionWinner != null)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var activeBids = auction.Bids.Where(b => b.BidStatus == BidStatus.Active).ToList();

            if (activeBids.Count > 0)
            {
                // Tie-breaking rule: highest amount, then earliest BidTimeUtc
                var winningBid = activeBids
                    .OrderByDescending(b => b.Amount)
                    .ThenBy(b => b.BidTimeUtc)
                    .First();

                var winner = new AuctionWinner
                {
                    AuctionId = auction.Id,
                    CustomerProfileId = winningBid.CustomerProfileId,
                    WinningBidId = winningBid.Id,
                    FinalAmount = winningBid.Amount,
                    SelectedAtUtc = now
                };

                dbContext.AuctionWinners.Add(winner);
                auction.CurrentHighestBid = winningBid.Amount;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        });
    }
}
