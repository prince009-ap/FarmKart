using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.Common;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var auctions = await dbContext.Auctions.Include(a => a.CropListing).ThenInclude(l => l.Crop)
            .Where(a => a.FarmerProfileId == farmer.Id).OrderByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);
        return await Task.WhenAll(auctions.Select(a => MapAsync(a, cancellationToken)));
    }

    public async Task<FarmerAuctionResponse> GetAuctionAsync(Guid userId, Guid auctionId, CancellationToken cancellationToken = default)
    {
        var auction = await FindOwnedAsync(userId, auctionId, cancellationToken);
        return await MapAsync(auction, cancellationToken);
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

    private async Task<FarmerProfile> GetFarmerAsync(Guid userId, CancellationToken ct) => await dbContext.FarmerProfiles.FirstOrDefaultAsync(f => f.UserId == userId, ct) ?? throw new KeyNotFoundException("Farmer profile not found.");
    private async Task<Auction> FindOwnedAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var farmer = await GetFarmerAsync(userId, ct);
        return await dbContext.Auctions.Include(a => a.CropListing).ThenInclude(l => l.Crop).ThenInclude(c => c.StockTransactions).FirstOrDefaultAsync(a => a.Id == id && a.FarmerProfileId == farmer.Id, ct) ?? throw new KeyNotFoundException("Auction not found.");
    }
    private async Task<FarmerAuctionResponse> MapAsync(Auction auction, CancellationToken ct)
    {
        var total = await dbContext.CropStockTransactions.Where(t => t.CropId == auction.CropListing.CropId).SumAsync(t => (decimal?)t.QuantityInBaseUnit, ct) ?? 0m;
        var now = DateTime.UtcNow;
        var reserved = await dbContext.Auctions.Include(a => a.CropListing).Where(a => a.CropListing.CropId == auction.CropListing.CropId && ReservingStatuses.Contains(a.AuctionStatus) && (a.AuctionStatus == AuctionStatus.Draft || a.EndTimeUtc > now)).SumAsync(a => (decimal?)a.CropListing.QuantityForSale * (a.CropListing.Unit == MeasurementUnit.Ton ? 1000m : a.CropListing.Unit == MeasurementUnit.Quintal ? 100m : 1m), ct) ?? 0m;
        var kg = CropStockUnitConverter.ToKilograms(auction.CropListing.QuantityForSale, auction.CropListing.Unit);
        var status = GetEffectiveStatus(auction, now).ToString();
        return new(auction.Id, auction.CropListing.CropId, auction.CropListing.Crop.CropName, auction.CropListing.QuantityForSale, CropStockUnitConverter.Format(auction.CropListing.Unit), kg, total, reserved, total - reserved, auction.StartingPrice, auction.MinimumBidIncrement, auction.StartTimeUtc, auction.EndTimeUtc, status, auction.CropListing.Description, auction.CreatedAtUtc, auction.UpdatedAtUtc, now);
    }
}
