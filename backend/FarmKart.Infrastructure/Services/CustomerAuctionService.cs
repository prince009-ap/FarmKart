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

    public async Task<PagedCustomerAuctionResponse> GetMarketplaceAuctionsAsync(
        CustomerAuctionFilterRequest? filter = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        filter ??= new CustomerAuctionFilterRequest();
        int page = Math.Max(1, filter.Page);
        int pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var endingSoonThreshold = now.AddHours(24);

        var query = dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.FarmerProfile)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.Images)
            .Include(a => a.FarmerProfile)
            .Where(a => a.AuctionStatus != AuctionStatus.Cancelled && a.AuctionStatus != AuctionStatus.Draft)
            .AsQueryable();

        // --- Search (crop name, variety, cropType, farmer name) ---
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a =>
                a.CropListing.Crop.CropName.ToLower().Contains(search) ||
                (a.CropListing.Crop.Variety != null && a.CropListing.Crop.Variety.ToLower().Contains(search)) ||
                a.CropListing.Crop.CropType.ToLower().Contains(search) ||
                (a.FarmerProfile != null && a.FarmerProfile.FullName.ToLower().Contains(search)) ||
                (a.CropListing.Crop.FarmerProfile != null && a.CropListing.Crop.FarmerProfile.FullName.ToLower().Contains(search)));
        }

        // --- Category filter ---
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var category = filter.Category.Trim().ToLower();
            query = query.Where(a => a.CropListing.Crop.CropType.ToLower() == category);
        }

        // --- Location filter (FarmLocation textual) ---
        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            var location = filter.Location.Trim().ToLower();
            query = query.Where(a =>
                (a.FarmerProfile != null && a.FarmerProfile.FarmLocation != null && a.FarmerProfile.FarmLocation.ToLower().Contains(location)) ||
                (a.CropListing.Crop.FarmerProfile != null && a.CropListing.Crop.FarmerProfile.FarmLocation != null && a.CropListing.Crop.FarmerProfile.FarmLocation.ToLower().Contains(location)));
        }

        // --- Price per Man filter (StartingPrice maps to StartingBidPrice in DTO) ---
        if (filter.MinPricePerMan.HasValue)
        {
            query = query.Where(a => a.StartingPrice >= filter.MinPricePerMan.Value);
        }
        if (filter.MaxPricePerMan.HasValue)
        {
            query = query.Where(a => a.StartingPrice <= filter.MaxPricePerMan.Value);
        }

        // --- Quantity filter (CropListing.QuantityForSale in Kg) ---
        if (filter.MinQuantityKg.HasValue)
        {
            query = query.Where(a => a.CropListing.QuantityForSale >= filter.MinQuantityKg.Value);
        }
        if (filter.MaxQuantityKg.HasValue)
        {
            query = query.Where(a => a.CropListing.QuantityForSale <= filter.MaxQuantityKg.Value);
        }

        // --- Ending Soon filter (live + ending within 24h) ---
        if (filter.EndingSoon == true)
        {
            query = query.Where(a => a.StartTimeUtc <= now && a.EndTimeUtc > now && a.EndTimeUtc <= endingSoonThreshold);
        }

        // --- Status filter (overrides EndingSoon if both provided) ---
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToUpper();
            switch (status)
            {
                case "LIVE":
                    query = query.Where(a => a.StartTimeUtc <= now && now <= a.EndTimeUtc);
                    break;
                case "UPCOMING":
                    query = query.Where(a => now < a.StartTimeUtc);
                    break;
                case "ENDED":
                    query = query.Where(a => now > a.EndTimeUtc);
                    break;
                case "ENDING_SOON":
                    query = query.Where(a => a.StartTimeUtc <= now && a.EndTimeUtc > now && a.EndTimeUtc <= endingSoonThreshold);
                    break;
            }
        }

        // --- Sorting (applied before pagination) ---
        var sortBy = (filter.SortBy ?? "newest").Trim().ToLower();
        query = sortBy switch
        {
            "ending_soon" => query.OrderBy(a => a.EndTimeUtc),
            "price_asc"   => query.OrderBy(a => a.StartingPrice),
            "price_desc"  => query.OrderByDescending(a => a.StartingPrice),
            "highest_bid" => query.OrderByDescending(a => a.CurrentHighestBid),
            "oldest"      => query.OrderBy(a => a.CreatedAtUtc),
            _             => query.OrderByDescending(a => a.CreatedAtUtc)  // newest
        };

        // --- Pagination ---
        var totalCount = await query.CountAsync(cancellationToken);
        var auctions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // --- Wishlist state injection ---
        HashSet<Guid> favoritedIds = [];
        if (!string.IsNullOrWhiteSpace(userId))
        {
            favoritedIds = (await dbContext.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId && w.ItemType == Domain.Enums.WishlistItemType.Auction)
                .Select(w => w.ItemId)
                .ToListAsync(cancellationToken)).ToHashSet();
        }

        var items = auctions.Select(a => MapToResponse(a, now, favoritedIds.Contains(a.Id))).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedCustomerAuctionResponse(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<CustomerAuctionResponse> GetAuctionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.FarmerProfile)
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
                    .ThenInclude(c => c.Images)
            .Include(a => a.FarmerProfile)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (auction == null || auction.AuctionStatus == AuctionStatus.Cancelled || auction.AuctionStatus == AuctionStatus.Draft)
        {
            throw new KeyNotFoundException($"Marketplace auction with ID '{id}' was not found.");
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
            .Include(a => a.CropListing)
                .ThenInclude(l => l.Crop)
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

        var availableKg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
        var requestedKg = request.RequestedQuantityKg.HasValue && request.RequestedQuantityKg.Value > 0
            ? request.RequestedQuantityKg.Value
            : availableKg;

        if (requestedKg <= 0)
        {
            throw new InvalidOperationException("Requested quantity must be greater than zero.");
        }

        if (requestedKg > availableKg)
        {
            throw new InvalidOperationException($"Requested quantity exceeds the available auction quantity. Available: {availableKg:0.##} Kg, Requested: {requestedKg:0.##} Kg.");
        }

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
                RequestedQuantityKg = requestedKg,
                BidTimeUtc = now,
                BidStatus = BidStatus.Active
            };

            dbContext.Bids.Add(newBid);

            auction.CurrentHighestBid = request.Amount;
            if (auction.AuctionStatus == AuctionStatus.Scheduled && now >= auction.StartTimeUtc)
            {
                auction.AuctionStatus = AuctionStatus.Live;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var requestedMan = AuctionPricingConstants.ConvertKgToMan(requestedKg);

            return new AuctionBidResponse(
                Id: newBid.Id,
                AuctionId: auctionId,
                CustomerProfileId: customerProfile.Id,
                CustomerName: customerProfile.FullName,
                Amount: newBid.Amount,
                RequestedQuantityKg: requestedKg,
                RequestedQuantityMan: requestedMan,
                BidTimeUtc: newBid.BidTimeUtc,
                BidStatus: "HIGHEST BID"
            );
        });
    }

    public async Task<IReadOnlyList<AuctionBidResponse>> GetAuctionBidsAsync(
        Guid auctionId,
        string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(a => a.CropListing)
            .Include(a => a.Allocations)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        var totalAuctionKg = auction?.CropListing != null
            ? CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit)
            : 0m;

        var query = dbContext.Bids
            .AsNoTracking()
            .Include(b => b.CustomerProfile)
            .Where(b => b.AuctionId == auctionId && b.BidStatus != BidStatus.Cancelled);

        var sortedQuery = sortBy?.ToLowerInvariant() switch
        {
            "lowest_bid" => query.OrderBy(b => b.Amount).ThenBy(b => b.BidTimeUtc),
            "highest_qty" => query.OrderByDescending(b => b.RequestedQuantityKg > 0 ? b.RequestedQuantityKg : totalAuctionKg).ThenByDescending(b => b.Amount),
            "latest_bid" => query.OrderByDescending(b => b.BidTimeUtc),
            "earliest_bid" => query.OrderBy(b => b.BidTimeUtc),
            _ => query.OrderByDescending(b => b.Amount).ThenBy(b => b.BidTimeUtc)
        };

        var bids = await sortedQuery.ToListAsync(cancellationToken);
        var highestAmount = bids.Count > 0 ? bids.Max(b => b.Amount) : 0m;
        var allocations = (auction?.Allocations ?? []).ToDictionary(a => a.BidId);

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

            return new AuctionBidResponse(
                Id: b.Id,
                AuctionId: b.AuctionId,
                CustomerProfileId: b.CustomerProfileId,
                CustomerName: b.CustomerProfile.FullName,
                Amount: b.Amount,
                RequestedQuantityKg: reqKg,
                RequestedQuantityMan: reqMan,
                BidTimeUtc: b.BidTimeUtc,
                BidStatus: b.Amount == highestAmount ? "HIGHEST BID" : "VALID",
                AllocationStatus: allocStatus
            );
        }).ToList();
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
            .Include(b => b.Auction)
                .ThenInclude(a => a.Allocations)
            .Where(b => b.CustomerProfileId == customerProfile.Id)
            .OrderByDescending(b => b.BidTimeUtc)
            .ToListAsync(cancellationToken);

        var groupedBids = bids
            .GroupBy(b => b.AuctionId)
            .Select(g =>
            {
                var winningBid = g.FirstOrDefault(b => b.Auction.Allocations.Any(a => a.BidId == b.Id && a.AllocatedQuantityKg > 0));
                return winningBid ?? g.OrderByDescending(b => b.BidTimeUtc).First();
            })
            .OrderByDescending(b => b.BidTimeUtc);

        var result = new List<CustomerMyBidResponse>();

        foreach (var bid in groupedBids)
        {
            var auction = bid.Auction;
            var crop = auction.CropListing.Crop;

            string auctionComputedStatus;
            if (auction.AuctionStatus == AuctionStatus.Cancelled)
            {
                auctionComputedStatus = "CANCELLED";
            }
            else if (now < auction.StartTimeUtc)
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

            var reqKg = bid.RequestedQuantityKg > 0 ? bid.RequestedQuantityKg : kgForBid;
            var reqMan = AuctionPricingConstants.ConvertKgToMan(reqKg);

            var allocation = auction.Allocations.FirstOrDefault(a => a.BidId == bid.Id)
                ?? auction.Allocations.FirstOrDefault(a => a.CustomerProfileId == customerProfile.Id && (a.Status == AllocationStatus.Won || a.Status == AllocationStatus.PartiallyWon));

            decimal? allocKg = allocation?.AllocatedQuantityKg;
            decimal? allocMan = allocKg.HasValue ? AuctionPricingConstants.ConvertKgToMan(allocKg.Value) : null;
            string? allocStatus = null;
            if (allocation != null)
            {
                if (allocation.Status == AllocationStatus.Won || (allocKg.HasValue && allocKg.Value >= kgForBid))
                {
                    allocStatus = "WON";
                }
                else if (allocation.Status == AllocationStatus.PartiallyWon || (allocKg.HasValue && allocKg.Value > 0))
                {
                    allocStatus = "PARTIALLY_WON";
                }
                else
                {
                    allocStatus = "LOST";
                }
            }

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
                RequestedQuantityKg: reqKg,
                RequestedQuantityMan: reqMan,
                CustomerBidAmount: bid.Amount,
                CurrentHighestBid: auction.CurrentHighestBid,
                MinimumBidIncrement: auction.MinimumBidIncrement,
                AllocatedQuantityKg: allocKg,
                AllocatedQuantityMan: allocMan,
                AuctionStatus: auctionComputedStatus,
                CustomerBidStatus: customerBidStatus,
                AllocationStatus: allocStatus,
                BidTimeUtc: bid.BidTimeUtc,
                StartTimeUtc: auction.StartTimeUtc,
                EndTimeUtc: auction.EndTimeUtc,
                ServerTimeUtc: now
            ));
        }

        return result;
    }

    private static CustomerAuctionResponse MapToResponse(Auction auction, DateTime now, bool isFavorited = false)
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
            FarmerName: farmer?.FullName ?? "Farmer",
            FarmLocation: farmer?.FarmLocation ?? "Location N/A",
            StartTimeUtc: auction.StartTimeUtc,
            EndTimeUtc: auction.EndTimeUtc,
            Status: computedStatus,
            PrimaryImageUrl: primaryImage,
            Images: images,
            Description: auction.CropListing.Description ?? crop.Description,
            CreatedAtUtc: auction.CreatedAtUtc,
            ServerTimeUtc: now,
            IsFavorited: isFavorited
        );
    }
}
