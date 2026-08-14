using System.Data;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class CustomerAuctionService(FarmKartDbContext dbContext) : ICustomerAuctionService
{
    public async Task<IReadOnlyList<CustomerAuctionResponse>> GetMarketplaceAuctionsAsync(
        CustomerAuctionFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var query = dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.FarmerProfile)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.Images)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.FarmerProfile)
            .Where(a => a.AuctionStatus != AuctionStatus.Cancelled && a.AuctionStatus != AuctionStatus.Draft);

        var auctions = await query.ToListAsync(cancellationToken);

        var responseList = auctions.Select(a => MapToResponse(a, now)).ToList();

        // Apply Search filter
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            responseList = responseList.Where(a =>
                a.CropName.ToLower().Contains(search) ||
                (a.Variety != null && a.Variety.ToLower().Contains(search)) ||
                a.CropType.ToLower().Contains(search) ||
                a.FarmerName.ToLower().Contains(search) ||
                a.FarmLocation.ToLower().Contains(search)
            ).ToList();
        }

        // Apply Category filter
        if (!string.IsNullOrWhiteSpace(filter.Category) && !filter.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var category = filter.Category.Trim().ToLower();
            responseList = responseList.Where(a => a.CropType.ToLower() == category).ToList();
        }

        // Apply Status filter (LIVE, UPCOMING, ENDED)
        if (!string.IsNullOrWhiteSpace(filter.Status) && !filter.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var status = filter.Status.Trim().ToUpper();
            responseList = responseList.Where(a => a.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Apply Location filter
        if (!string.IsNullOrWhiteSpace(filter.Location) && !filter.Location.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var loc = filter.Location.Trim().ToLower();
            responseList = responseList.Where(a => a.FarmLocation.ToLower().Contains(loc)).ToList();
        }

        // Apply Sorting
        responseList = (filter.SortBy?.ToLower()) switch
        {
            "ending_soon" => responseList.OrderBy(a => a.EndTimeUtc).ToList(),
            "price_asc" => responseList.OrderBy(a => a.StartingBidPrice).ToList(),
            "price_desc" => responseList.OrderByDescending(a => a.StartingBidPrice).ToList(),
            _ => responseList.OrderByDescending(a => a.CreatedAtUtc).ToList()
        };

        return responseList;
    }

    public async Task<CustomerAuctionResponse> GetAuctionByIdAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.FarmerProfile)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.Images)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.FarmerProfile)
            .FirstOrDefaultAsync(a => a.Id == auctionId && a.AuctionStatus != AuctionStatus.Cancelled && a.AuctionStatus != AuctionStatus.Draft, cancellationToken);

        if (auction == null)
        {
            throw new KeyNotFoundException($"Auction with ID '{auctionId}' was not found.");
        }

        return MapToResponse(auction, now);
    }

    public async Task<AuctionBidResponse> PlaceBidAsync(
        Guid userId,
        Guid auctionId,
        PlaceBidRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");
        }

        var auction = await dbContext.Auctions
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction == null || auction.AuctionStatus == AuctionStatus.Cancelled || auction.AuctionStatus == AuctionStatus.Draft)
        {
            throw new KeyNotFoundException($"Live auction with ID '{auctionId}' was not found.");
        }

        var now = DateTime.UtcNow;
        if (now < auction.StartTimeUtc)
        {
            throw new InvalidOperationException("Auction has not started yet. Bids are only accepted when the auction is LIVE.");
        }

        if (now > auction.EndTimeUtc)
        {
            throw new InvalidOperationException("Auction has ended. Bids are no longer accepted.");
        }

        // Concurrency-safe bid evaluation using database transaction
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var activeBids = await dbContext.Bids
                .Where(b => b.AuctionId == auctionId && b.BidStatus == BidStatus.Active)
                .ToListAsync(cancellationToken);

            var currentHighest = activeBids.Count > 0 ? activeBids.Max(b => b.Amount) : 0m;

            if (activeBids.Count == 0)
            {
                if (request.Amount < auction.StartingPrice)
                {
                    throw new InvalidOperationException($"First bid must be at least the starting price of ₹{auction.StartingPrice:0.##}.");
                }
            }
            else
            {
                var minRequired = currentHighest + auction.MinimumBidIncrement;
                if (request.Amount < minRequired)
                {
                    throw new InvalidOperationException($"Bid amount must be at least ₹{minRequired:0.##} (current highest ₹{currentHighest:0.##} + increment ₹{auction.MinimumBidIncrement:0.##}).");
                }
            }

            // Verify minimum increment step requirement relative to starting price
            var delta = request.Amount - auction.StartingPrice;
            if (delta < 0 || (auction.MinimumBidIncrement > 0 && Math.Abs((delta % auction.MinimumBidIncrement)) > 0.0001m))
            {
                throw new InvalidOperationException($"Bid amount must be in valid increment steps of ₹{auction.MinimumBidIncrement:0.##} from starting price ₹{auction.StartingPrice:0.##}.");
            }

            var newBid = new Bid
            {
                AuctionId = auctionId,
                CustomerProfileId = customerProfile.Id,
                Amount = request.Amount,
                BidTimeUtc = now,
                BidStatus = BidStatus.Active
            };

            dbContext.Bids.Add(newBid);

            // Update current highest bid on auction entity
            auction.CurrentHighestBid = request.Amount;
            if (auction.AuctionStatus == AuctionStatus.Scheduled && now >= auction.StartTimeUtc)
            {
                auction.AuctionStatus = AuctionStatus.Live;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AuctionBidResponse(
                Id: newBid.Id,
                AuctionId: auctionId,
                CustomerProfileId: customerProfile.Id,
                CustomerName: customerProfile.FullName,
                Amount: newBid.Amount,
                BidTimeUtc: newBid.BidTimeUtc,
                BidStatus: "HIGHEST BID"
            );
        });
    }

    public async Task<IReadOnlyList<AuctionBidResponse>> GetAuctionBidsAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        var bids = await dbContext.Bids
            .AsNoTracking()
            .Include(b => b.CustomerProfile)
            .Where(b => b.AuctionId == auctionId && b.BidStatus == BidStatus.Active)
            .OrderByDescending(b => b.BidTimeUtc)
            .ToListAsync(cancellationToken);

        var highestAmount = bids.Count > 0 ? bids.Max(b => b.Amount) : 0m;

        return bids.Select(b => new AuctionBidResponse(
            Id: b.Id,
            AuctionId: b.AuctionId,
            CustomerProfileId: b.CustomerProfileId,
            CustomerName: b.CustomerProfile.FullName,
            Amount: b.Amount,
            BidTimeUtc: b.BidTimeUtc,
            BidStatus: b.Amount == highestAmount ? "HIGHEST BID" : "OUTBID"
        )).ToList();
    }

    public async Task<IReadOnlyList<CustomerMyBidResponse>> GetCustomerBidsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var customerProfile = await dbContext.CustomerProfiles
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            throw new UnauthorizedAccessException("Customer profile not found for authenticated user.");
        }

        var now = DateTime.UtcNow;

        var bids = await dbContext.Bids
            .AsNoTracking()
            .Include(b => b.Auction)
                .ThenInclude(a => a.CropListing)
                    .ThenInclude(l => l.Crop)
                        .ThenInclude(c => c.Images)
            .Where(b => b.CustomerProfileId == customerProfile.Id && b.BidStatus == BidStatus.Active)
            .OrderByDescending(b => b.BidTimeUtc)
            .ToListAsync(cancellationToken);

        var result = new List<CustomerMyBidResponse>();

        foreach (var bid in bids)
        {
            var auction = bid.Auction;
            var crop = auction.CropListing.Crop;

            string auctionComputedStatus;
            if (now < auction.StartTimeUtc)
            {
                auctionComputedStatus = "UPCOMING";
            }
            else if (now <= auction.EndTimeUtc)
            {
                auctionComputedStatus = "LIVE";
            }
            else
            {
                auctionComputedStatus = "ENDED";
            }

            var images = crop.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList();
            var primaryImage = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? images.FirstOrDefault();

            var customerBidStatus = bid.Amount == auction.CurrentHighestBid ? "HIGHEST BID" : "OUTBID";
            var kgForBid = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
            var manForBid = AuctionPricingConstants.ConvertKgToMan(kgForBid);

            result.Add(new CustomerMyBidResponse(
                BidId: bid.Id,
                AuctionId: auction.Id,
                CropId: crop.Id,
                CropName: crop.CropName,
                PrimaryImageUrl: primaryImage,
                CropType: crop.CropType,
                Quantity: auction.CropListing.QuantityForSale,
                Unit: CropStockUnitConverter.Format(auction.CropListing.Unit),
                QuantityMan: manForBid,
                CustomerBidAmount: bid.Amount,
                CurrentHighestBid: auction.CurrentHighestBid,
                MinimumBidIncrement: auction.MinimumBidIncrement,
                AuctionStatus: auctionComputedStatus,
                CustomerBidStatus: customerBidStatus,
                BidTimeUtc: bid.BidTimeUtc,
                StartTimeUtc: auction.StartTimeUtc,
                EndTimeUtc: auction.EndTimeUtc,
                ServerTimeUtc: now
            ));
        }

        return result;
    }

    private static CustomerAuctionResponse MapToResponse(Auction auction, DateTime now)
    {
        var crop = auction.CropListing.Crop;
        var farmer = crop.FarmerProfile ?? auction.FarmerProfile;

        string computedStatus;
        if (auction.AuctionStatus == AuctionStatus.Cancelled)
        {
            computedStatus = "CANCELLED";
        }
        else if (now < auction.StartTimeUtc)
        {
            computedStatus = "UPCOMING";
        }
        else if (now >= auction.EndTimeUtc)
        {
            computedStatus = "ENDED";
        }
        else
        {
            computedStatus = "LIVE";
        }

        var images = crop.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList();
        var primaryImage = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? images.FirstOrDefault();

        var kg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
        var man = AuctionPricingConstants.ConvertKgToMan(kg);
        var unitFormatted = CropStockUnitConverter.Format(auction.CropListing.Unit);

        return new CustomerAuctionResponse(
            Id: auction.Id,
            CropId: crop.Id,
            CropName: crop.CropName,
            CropType: crop.CropType,
            Variety: crop.Variety,
            Quantity: auction.CropListing.QuantityForSale,
            Unit: unitFormatted,
            QuantityKg: kg,
            QuantityMan: man,
            StartingBidPrice: auction.StartingPrice,
            CurrentHighestBid: auction.CurrentHighestBid,
            MinimumBidIncrement: auction.MinimumBidIncrement,
            FarmerName: farmer.FullName,
            FarmLocation: farmer.FarmLocation,
            StartTimeUtc: auction.StartTimeUtc,
            EndTimeUtc: auction.EndTimeUtc,
            Status: computedStatus,
            PrimaryImageUrl: primaryImage,
            Images: images,
            Description: auction.CropListing.Description ?? crop.Description,
            CreatedAtUtc: auction.CreatedAtUtc,
            ServerTimeUtc: now
        );
    }
}
