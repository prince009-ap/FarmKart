using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
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
            _ => responseList.OrderByDescending(a => a.CreatedAtUtc).ToList() // "newest" or default
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

    private static CustomerAuctionResponse MapToResponse(Auction auction, DateTime now)
    {
        var crop = auction.CropListing.Crop;
        var farmer = crop.FarmerProfile ?? auction.FarmerProfile;

        string computedStatus;
        if (now < auction.StartTimeUtc)
        {
            computedStatus = "UPCOMING";
        }
        else if (now <= auction.EndTimeUtc)
        {
            computedStatus = "LIVE";
        }
        else
        {
            computedStatus = "ENDED";
        }

        var images = crop.Images.OrderBy(i => i.DisplayOrder).Select(i => i.ImageUrl).ToList();
        var primaryImage = crop.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? images.FirstOrDefault();

        var kg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
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
            CreatedAtUtc: auction.CreatedAtUtc
        );
    }
}
