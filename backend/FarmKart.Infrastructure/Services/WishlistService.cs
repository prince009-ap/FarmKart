using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FarmKart.Infrastructure.Services;

public sealed class WishlistService(FarmKartDbContext dbContext) : IWishlistService
{
    public async Task<WishlistItemResponse> AddAsync(string userId, AddWishlistItemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User not authenticated.");

        await ValidateItemExistsAsync(request.ItemType, request.ItemId, cancellationToken);

        var existing = await dbContext.WishlistItems
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ItemType == request.ItemType && w.ItemId == request.ItemId, cancellationToken);

        if (existing != null)
        {
            return await BuildResponseAsync(existing, cancellationToken);
        }

        var item = new WishlistItem
        {
            UserId = userId,
            ItemType = request.ItemType,
            ItemId = request.ItemId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.WishlistItems.Add(item);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_WishlistItems_UserId_ItemType_ItemId") == true
                                         || ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            var race = await dbContext.WishlistItems
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.UserId == userId && w.ItemType == request.ItemType && w.ItemId == request.ItemId, cancellationToken);
            return await BuildResponseAsync(race!, cancellationToken);
        }

        return await BuildResponseAsync(item, cancellationToken);
    }

    public async Task RemoveAsync(string userId, WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User not authenticated.");

        var item = await dbContext.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ItemType == itemType && w.ItemId == itemId, cancellationToken);

        if (item == null)
            throw new KeyNotFoundException("Wishlist item not found.");

        dbContext.WishlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WishlistItemResponse>> GetWishlistAsync(string userId, WishlistItemType? itemType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User not authenticated.");

        await CleanUpEndedAuctionsAsync(userId, cancellationToken);

        var query = dbContext.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId);

        if (itemType.HasValue)
            query = query.Where(w => w.ItemType == itemType.Value);

        var items = await query.OrderByDescending(w => w.CreatedAtUtc).ToListAsync(cancellationToken);

        var results = new List<WishlistItemResponse>(items.Count);
        foreach (var item in items)
        {
            var resp = await BuildResponseAsync(item, cancellationToken);
            if (resp.IsItemAvailable)
            {
                results.Add(resp);
            }
        }

        return results;
    }

    public async Task<WishlistCountResponse> GetCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User not authenticated.");

        await CleanUpEndedAuctionsAsync(userId, cancellationToken);

        var groups = await dbContext.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .GroupBy(w => w.ItemType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var cropCount = groups.FirstOrDefault(g => g.Type == WishlistItemType.Crop)?.Count ?? 0;
        var auctionCount = groups.FirstOrDefault(g => g.Type == WishlistItemType.Auction)?.Count ?? 0;
        var machineryCount = groups.FirstOrDefault(g => g.Type == WishlistItemType.Machinery)?.Count ?? 0;
        var total = groups.Sum(g => g.Count);

        return new WishlistCountResponse { Total = total, CropCount = cropCount, AuctionCount = auctionCount, MachineryCount = machineryCount };
    }

    public async Task<WishlistStatusResponse> GetItemStatusAsync(string userId, WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("User not authenticated.");

        var item = await dbContext.WishlistItems
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ItemType == itemType && w.ItemId == itemId, cancellationToken);

        return item == null
            ? new WishlistStatusResponse { IsFavorited = false, WishlistItemId = null }
            : new WishlistStatusResponse { IsFavorited = true, WishlistItemId = item.Id };
    }

    public async Task<HashSet<Guid>> GetFavoritedItemIdsAsync(string userId, WishlistItemType itemType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        return (await dbContext.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.ItemType == itemType)
            .Select(w => w.ItemId)
            .ToListAsync(cancellationToken)).ToHashSet();
    }

    private async Task CleanUpEndedAuctionsAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var endedAuctionWishlistItems = await (
            from w in dbContext.WishlistItems
            join a in dbContext.Auctions on w.ItemId equals a.Id
            where w.UserId == userId && w.ItemType == WishlistItemType.Auction &&
                  (a.EndTimeUtc <= now || a.AuctionStatus == AuctionStatus.Ended || a.AuctionStatus == AuctionStatus.Cancelled)
            select w
        ).ToListAsync(cancellationToken);

        if (endedAuctionWishlistItems.Count > 0)
        {
            dbContext.WishlistItems.RemoveRange(endedAuctionWishlistItems);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ValidateItemExistsAsync(WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (itemType == WishlistItemType.Crop)
        {
            var cropExists = await dbContext.Crops
                .AsNoTracking()
                .AnyAsync(c => c.Id == itemId && c.Status != CropStatus.Archived, cancellationToken);

            if (!cropExists)
                throw new KeyNotFoundException($"Crop with ID '{itemId}' was not found or is unavailable.");
        }
        else if (itemType == WishlistItemType.Auction)
        {
            var auction = await dbContext.Auctions
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == itemId, cancellationToken);

            if (auction == null)
                throw new KeyNotFoundException($"Auction with ID '{itemId}' was not found.");

            if (now >= auction.EndTimeUtc || auction.AuctionStatus == AuctionStatus.Ended || auction.AuctionStatus == AuctionStatus.Cancelled)
            {
                throw new InvalidOperationException("Ended or cancelled auctions cannot be added to wishlist.");
            }
        }
        else if (itemType == WishlistItemType.Machinery)
        {
            var machineryExists = await dbContext.Machinery
                .AsNoTracking()
                .AnyAsync(m => m.Id == itemId && m.IsActive, cancellationToken);

            if (!machineryExists)
                throw new KeyNotFoundException($"Machinery with ID '{itemId}' was not found or is inactive.");
        }
        else
        {
            throw new ArgumentException($"Unsupported item type: {itemType}");
        }
    }

    private async Task<WishlistItemResponse> BuildResponseAsync(WishlistItem item, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (item.ItemType == WishlistItemType.Crop)
        {
            var crop = await dbContext.Crops
                .AsNoTracking()
                .Include(c => c.FarmerProfile)
                .Include(c => c.Images)
                .FirstOrDefaultAsync(c => c.Id == item.ItemId, cancellationToken);

            if (crop == null)
            {
                return new WishlistItemResponse
                {
                    Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                    IsAuctionExpired = false, IsItemAvailable = false
                };
            }

            var primaryImage = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? crop.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();

            return new WishlistItemResponse
            {
                Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                CropName = crop.CropName, CropType = crop.CropType, Variety = crop.Variety,
                FarmerName = crop.FarmerProfile?.FullName, PrimaryImageUrl = primaryImage,
                CropStatus = crop.Status.ToString(),
                IsAuctionExpired = false, IsItemAvailable = crop.Status != CropStatus.Archived
            };
        }
        else if (item.ItemType == WishlistItemType.Auction)
        {
            var auction = await dbContext.Auctions
                .AsNoTracking()
                .Include(a => a.CropListing).ThenInclude(l => l.Crop).ThenInclude(c => c.FarmerProfile)
                .Include(a => a.CropListing).ThenInclude(l => l.Crop).ThenInclude(c => c.Images)
                .Include(a => a.FarmerProfile)
                .FirstOrDefaultAsync(a => a.Id == item.ItemId, cancellationToken);

            if (auction == null || auction.AuctionStatus == AuctionStatus.Cancelled || now >= auction.EndTimeUtc || auction.AuctionStatus == AuctionStatus.Ended)
            {
                return new WishlistItemResponse
                {
                    Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                    IsAuctionExpired = true, IsItemAvailable = false
                };
            }

            var crop = auction.CropListing.Crop;
            var farmer = crop.FarmerProfile ?? auction.FarmerProfile;
            var primaryImage = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? crop.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();

            var computedStatus = now < auction.StartTimeUtc ? "UPCOMING" : "LIVE";

            var kg = auction.CropListing.QuantityForSale;
            var man = kg / 20m;

            return new WishlistItemResponse
            {
                Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                CropName = crop.CropName, CropType = crop.CropType, Variety = crop.Variety,
                FarmerName = farmer?.FullName, PrimaryImageUrl = primaryImage,
                AuctionStatus = computedStatus,
                StartingBidPrice = auction.StartingPrice,
                CurrentHighestBid = auction.CurrentHighestBid,
                QuantityKg = kg, QuantityMan = man,
                AuctionStartTimeUtc = auction.StartTimeUtc, AuctionEndTimeUtc = auction.EndTimeUtc,
                ServerTimeUtc = now,
                IsAuctionExpired = false, IsItemAvailable = true
            };
        }
        else if (item.ItemType == WishlistItemType.Machinery)
        {
            var machinery = await dbContext.Machinery
                .AsNoTracking()
                .Include(m => m.Images)
                .FirstOrDefaultAsync(m => m.Id == item.ItemId, cancellationToken);

            if (machinery == null || !machinery.IsActive)
            {
                return new WishlistItemResponse
                {
                    Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                    IsItemAvailable = false
                };
            }

            // Resolve owner name
            var ownerGuid = Guid.TryParse(machinery.OwnerUserId, out var og) ? og : Guid.Empty;
            var ownerName = (await dbContext.FarmerProfiles.AsNoTracking()
                .FirstOrDefaultAsync(fp => fp.UserId == ownerGuid, cancellationToken))?.FullName
                ?? (await dbContext.CustomerProfiles.AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.UserId == ownerGuid, cancellationToken))?.FullName
                ?? "Owner";

            var primaryImage = machinery.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                ?? machinery.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).FirstOrDefault();

            return new WishlistItemResponse
            {
                Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                MachineryName = machinery.Name,
                MachineryCategory = machinery.Category,
                MachineryStatus = machinery.AvailabilityStatus.ToString(),
                MachineryDailyRent = machinery.DailyRent,
                MachineryPrimaryImageUrl = primaryImage,
                MachineryOwnerName = ownerName,
                IsItemAvailable = true
            };
        }
        else // Unknown type - return as unavailable
        {
            return new WishlistItemResponse
            {
                Id = item.Id, ItemType = item.ItemType, ItemId = item.ItemId, CreatedAtUtc = item.CreatedAtUtc,
                IsItemAvailable = false
            };
        }
    }
}
