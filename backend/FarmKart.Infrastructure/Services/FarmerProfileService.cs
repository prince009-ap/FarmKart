using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FarmKart.Application.Abstractions.Profile;

namespace FarmKart.Infrastructure.Services;

public sealed class FarmerProfileService : IFarmerProfileService
{
    private readonly FarmKartDbContext _db;
    private readonly IProfileImageService _profileImageService;

    public FarmerProfileService(FarmKartDbContext db, IProfileImageService profileImageService)
    {
        _db = db;
        _profileImageService = profileImageService;
    }

    public async Task<FarmerProfileResponse> GetProfileAsync(Guid userId)
    {
        var farmer = await _db.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId);

        if (farmer == null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        return new FarmerProfileResponse(
            UserId: farmer.UserId,
            FullName: farmer.FullName,
            Email: user?.Email ?? string.Empty,
            Phone: farmer.Phone,
            Address: farmer.AddressInfo?.AddressLine ?? string.Empty,
            FarmName: farmer.FarmName,
            FarmSize: farmer.FarmSize,
            FarmSizeUnit: farmer.FarmSizeUnit,
            FarmLocation: farmer.FarmLocation,
            ProfileImageUrl: farmer.ProfileImageUrl
        );
    }

    public async Task<FarmerProfileResponse> UpdateProfileAsync(Guid userId, FarmerProfileUpdateRequest request)
    {
        var farmer = await _db.FarmerProfiles
            .FirstOrDefaultAsync(f => f.UserId == userId);

        if (farmer == null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName)) farmer.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone)) farmer.Phone = request.Phone.Trim();
        if (request.Address != null) farmer.AddressInfo.AddressLine = request.Address.Trim();
        if (request.FarmName != null) farmer.FarmName = request.FarmName.Trim();
        if (request.FarmSize.HasValue) farmer.FarmSize = request.FarmSize;
        if (request.FarmSizeUnit.HasValue) farmer.FarmSizeUnit = request.FarmSizeUnit;
        if (request.FarmLocation != null) farmer.FarmLocation = request.FarmLocation.Trim();

        await _db.SaveChangesAsync();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        return new FarmerProfileResponse(
            UserId: farmer.UserId,
            FullName: farmer.FullName,
            Email: user?.Email ?? string.Empty,
            Phone: farmer.Phone,
            Address: farmer.AddressInfo?.AddressLine ?? string.Empty,
            FarmName: farmer.FarmName,
            FarmSize: farmer.FarmSize,
            FarmSizeUnit: farmer.FarmSizeUnit,
            FarmLocation: farmer.FarmLocation,
            ProfileImageUrl: farmer.ProfileImageUrl
        );
    }

    public async Task<FarmerProfileResponse> UploadProfileImageAsync(
        Guid userId,
        Stream stream,
        string fileName,
        string contentType,
        long fileLength,
        CancellationToken cancellationToken = default)
    {
        var farmer = await _db.FarmerProfiles
            .FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);

        if (farmer == null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        var newImageUrl = await _profileImageService.UploadProfileImageAsync(
            userId, stream, fileName, contentType, fileLength, farmer.ProfileImageUrl, cancellationToken);

        farmer.ProfileImageUrl = newImageUrl;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(userId);
    }

    public async Task<FarmerProfileResponse> RemoveProfileImageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var farmer = await _db.FarmerProfiles
            .FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);

        if (farmer == null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        if (!string.IsNullOrEmpty(farmer.ProfileImageUrl))
        {
            _profileImageService.DeleteProfileImage(farmer.ProfileImageUrl);
            farmer.ProfileImageUrl = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await GetProfileAsync(userId);
    }

    public async Task<FarmerPublicProfileResponse?> GetPublicFarmerProfileAsync(string farmerIdOrUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(farmerIdOrUserId))
            return null;

        FarmerProfile? farmer = null;

        if (Guid.TryParse(farmerIdOrUserId, out var targetGuid))
        {
            farmer = await _db.FarmerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == targetGuid || f.UserId == targetGuid, cancellationToken);
        }

        if (farmer == null)
        {
            if (Guid.TryParse(farmerIdOrUserId, out var custGuid))
            {
                var customer = await _db.CustomerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == custGuid || c.UserId == custGuid, cancellationToken);

                var user = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == custGuid, cancellationToken);

                if (customer != null || user != null)
                {
                    var ownerUserIdStr = (customer?.UserId ?? user?.Id)?.ToString() ?? farmerIdOrUserId;
                    var ownerName = customer?.FullName ?? user?.Email ?? "Equipment Owner";
                    var location = customer?.AddressInfo?.AddressLine ?? "Location N/A";
                    var custIdGuid = customer?.Id ?? user?.Id ?? custGuid;
                    var custUserIdGuid = customer?.UserId ?? user?.Id ?? custGuid;

                    var custMachineryList = await _db.Machinery
                        .AsNoTracking()
                        .Include(m => m.Images)
                        .Where(m => m.OwnerUserId == ownerUserIdStr && m.IsActive)
                        .OrderByDescending(m => m.CreatedAtUtc)
                        .ToListAsync(cancellationToken);

                    var custMachineryIds = custMachineryList.Select(m => m.Id).ToList();
                    var custRentals = await _db.MachineryRentals
                        .AsNoTracking()
                        .Where(r => custMachineryIds.Contains(r.MachineryId))
                        .Select(r => new { r.Id, r.MachineryId })
                        .ToListAsync(cancellationToken);

                    var custRentalGuids = custRentals.Select(r => r.Id).ToList();
                    var custMachineryReviews = await _db.Reviews
                        .AsNoTracking()
                        .Where(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId.HasValue && custRentalGuids.Contains(r.RelatedEntityId.Value))
                        .ToListAsync(cancellationToken);

                    var custMachineryReviewsMap = custMachineryReviews
                        .GroupBy(r => custRentals.FirstOrDefault(ren => ren.Id == r.RelatedEntityId.Value)?.MachineryId)
                        .Where(g => g.Key.HasValue)
                        .ToDictionary(g => g.Key!.Value, g => g.ToList());

                    var custMachineryResponses = custMachineryList.Select(m => {
                        var mReviews = custMachineryReviewsMap.TryGetValue(m.Id, out var rList) ? rList : [];
                        double mAvg = mReviews.Count > 0 ? Math.Round(mReviews.Average(r => r.Rating), 1) : 0.0;
                        var primaryImg = m.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? m.Images.FirstOrDefault()?.ImageUrl;

                        return new FarmerPublicMachineryResponse(
                            MachineryId: m.Id,
                            Name: m.Name,
                            Category: m.Category,
                            Brand: m.Brand,
                            Model: m.Model,
                            DailyRent: m.DailyRent,
                            DriverAvailable: m.DriverAvailable,
                            AvailabilityStatus: m.AvailabilityStatus.ToString(),
                            AverageRating: mAvg,
                            ReviewCount: mReviews.Count,
                            PrimaryImageUrl: primaryImg,
                            Location: m.Location,
                            City: m.City,
                            State: m.State
                        );
                    }).ToList();

                    return new FarmerPublicProfileResponse(
                        FarmerId: custIdGuid,
                        UserId: custUserIdGuid,
                        FullName: ownerName,
                        FarmName: "Equipment Owner",
                        Location: location,
                        City: customer?.AddressInfo?.City,
                        State: customer?.AddressInfo?.State,
                        MemberSinceUtc: DateTime.UtcNow,
                        AverageRating: 0.0,
                        TotalReviews: 0,
                        Reviews: Array.Empty<FarmerPublicReviewResponse>(),
                        ActiveAuctions: Array.Empty<FarmerPublicAuctionResponse>(),
                        Machinery: custMachineryResponses,
                        ProfileImageUrl: customer?.ProfileImageUrl
                    );
                }
            }

            return null;
        }

        var farmerUserIdStr = farmer.UserId.ToString();

        // 1. Calculate Farmer Rating & Reviews from Order Reviews
        var farmerReviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.RevieweeUserId == farmerUserIdStr && r.RelatedEntityType == ReviewEntityType.Order)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        double avgFarmerRating = farmerReviews.Count > 0 ? Math.Round(farmerReviews.Average(r => r.Rating), 1) : 0.0;
        int totalFarmerReviews = farmerReviews.Count;

        // Fetch Customer Names for Reviews
        var reviewerUserIds = farmerReviews.Select(r => r.ReviewerUserId).Distinct().ToList();
        var reviewerGuids = reviewerUserIds.Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();

        var customerProfiles = await _db.CustomerProfiles
            .AsNoTracking()
            .Where(c => reviewerGuids.Contains(c.UserId))
            .ToDictionaryAsync(c => c.UserId.ToString(), c => c.FullName, cancellationToken);

        var reviewResponses = farmerReviews.Select(r => new FarmerPublicReviewResponse(
            ReviewId: r.Id,
            ReviewerName: customerProfiles.TryGetValue(r.ReviewerUserId, out var name) ? name : "Verified Customer",
            Rating: r.Rating,
            Comment: r.Comment,
            CreatedAtUtc: r.CreatedAtUtc
        )).ToList();

        // 2. Active Auctions owned by this Farmer
        var auctions = await _db.Auctions
            .AsNoTracking()
            .Include(a => a.CropListing)
                .ThenInclude(cl => cl.Crop)
                    .ThenInclude(c => c.Images)
            .Where(a => a.FarmerProfileId == farmer.Id && a.AuctionStatus != AuctionStatus.Cancelled && a.AuctionStatus != AuctionStatus.Draft)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var auctionResponses = auctions.Select(a => {
            var crop = a.CropListing?.Crop;
            var primaryImage = crop?.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? crop?.Images?.FirstOrDefault()?.ImageUrl;
            return new FarmerPublicAuctionResponse(
                AuctionId: a.Id,
                Title: a.CropListing?.Crop?.CropName ?? "Auction Crop",
                CropName: a.CropListing?.Crop?.CropName ?? "Crop",
                CropType: a.CropListing?.Crop?.CropType ?? "Standard",
                StartingPrice: a.StartingPrice,
                TotalQuantity: a.CropListing?.QuantityForSale ?? 0m,
                Unit: a.CropListing?.Unit.ToString() ?? "Kilogram",
                Status: a.AuctionStatus.ToString(),
                StartDateUtc: a.StartTimeUtc,
                EndDateUtc: a.EndTimeUtc,
                PrimaryImageUrl: primaryImage
            );
        }).ToList();

        // 3. Listed Machinery owned by this Farmer
        var machineryList = await _db.Machinery
            .AsNoTracking()
            .Include(m => m.Images)
            .Where(m => m.OwnerUserId == farmerUserIdStr && m.IsActive)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var machineryIds = machineryList.Select(m => m.Id).ToList();

        // Get all completed rentals for these machinery items to map reviews
        var rentals = await _db.MachineryRentals
            .AsNoTracking()
            .Where(r => machineryIds.Contains(r.MachineryId))
            .Select(r => new { r.Id, r.MachineryId })
            .ToListAsync(cancellationToken);

        var rentalIdToMachineryId = rentals.ToDictionary(r => r.Id, r => r.MachineryId);
        var rentalGuids = rentals.Select(r => r.Id).ToList();

        var machineryReviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId.HasValue && rentalGuids.Contains(r.RelatedEntityId.Value))
            .ToListAsync(cancellationToken);

        var machineryRatingLookup = machineryReviews
            .GroupBy(r => rentalIdToMachineryId[r.RelatedEntityId!.Value])
            .ToDictionary(
                g => g.Key,
                g => new { AvgRating = Math.Round(g.Average(r => r.Rating), 1), Count = g.Count() }
            );

        var machineryResponses = machineryList.Select(m => {
            var stats = machineryRatingLookup.TryGetValue(m.Id, out var s) ? s : new { AvgRating = 0.0, Count = 0 };
            var img = m.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? m.Images?.FirstOrDefault()?.ImageUrl;
            return new FarmerPublicMachineryResponse(
                MachineryId: m.Id,
                Name: m.Name,
                Category: m.Category,
                Brand: m.Brand,
                Model: m.Model,
                DailyRent: m.DailyRent,
                DriverAvailable: m.DriverAvailable,
                AvailabilityStatus: m.AvailabilityStatus.ToString(),
                AverageRating: stats.AvgRating,
                ReviewCount: stats.Count,
                PrimaryImageUrl: img,
                Location: m.Location,
                City: m.City,
                State: m.State
            );
        }).ToList();

        return new FarmerPublicProfileResponse(
            FarmerId: farmer.Id,
            UserId: farmer.UserId,
            FullName: farmer.FullName,
            FarmName: farmer.FarmName,
            Location: farmer.FarmLocation ?? farmer.AddressInfo?.AddressLine,
            City: farmer.AddressInfo?.City,
            State: farmer.AddressInfo?.State,
            MemberSinceUtc: farmer.CreatedAtUtc,
            AverageRating: avgFarmerRating,
            TotalReviews: totalFarmerReviews,
            Reviews: reviewResponses,
            ActiveAuctions: auctionResponses,
            Machinery: machineryResponses,
            ProfileImageUrl: farmer.ProfileImageUrl
        );
    }
}
