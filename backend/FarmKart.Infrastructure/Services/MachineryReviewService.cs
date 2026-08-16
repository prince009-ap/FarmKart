using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.DTOs;
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

public sealed class MachineryReviewService : IMachineryReviewService
{
    private readonly FarmKartDbContext _db;

    public MachineryReviewService(FarmKartDbContext db)
    {
        _db = db;
    }

    public async Task<MachineryReviewResponse> CreateMachineryReviewAsync(string reviewerUserId, Guid rentalId, CreateMachineryReviewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRatingAndComment(request.Rating, request.Comment);

        if (string.IsNullOrWhiteSpace(reviewerUserId))
        {
            throw new UnauthorizedAccessException("Invalid reviewer user ID.");
        }

        var rental = await _db.MachineryRentals
            .Include(r => r.Machinery)
            .FirstOrDefaultAsync(r => r.Id == rentalId, cancellationToken);

        if (rental == null)
        {
            throw new KeyNotFoundException("Machinery rental not found.");
        }

        if (!string.Equals(rental.RenterUserId, reviewerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only the renter who completed this rental can review this machinery.");
        }

        if (rental.RentalStatus != RentalStatus.Completed)
        {
            throw new InvalidOperationException("Only completed machinery rentals can be reviewed.");
        }

        var existingReview = await _db.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId == rentalId, cancellationToken);

        if (existingReview != null)
        {
            throw new InvalidOperationException("A review has already been submitted for this machinery rental.");
        }

        var reviewerGuid = Guid.TryParse(reviewerUserId, out var g) ? g : Guid.Empty;
        var reviewerName = await GetUserDisplayNameAsync(reviewerGuid, cancellationToken);

        var review = new Review
        {
            ReviewerUserId = reviewerUserId,
            RevieweeUserId = rental.Machinery.OwnerUserId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            RelatedEntityType = ReviewEntityType.MachineryRental,
            RelatedEntityId = rentalId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        return new MachineryReviewResponse(
            ReviewId: review.Id,
            RentalId: rental.Id,
            MachineryId: rental.MachineryId,
            MachineryName: rental.Machinery.Name,
            ReviewerName: reviewerName,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAtUtc: review.CreatedAtUtc,
            UpdatedAtUtc: review.UpdatedAtUtc
        );
    }

    public async Task<MachineryReviewResponse?> GetRentalReviewAsync(string userId, Guid rentalId, CancellationToken cancellationToken = default)
    {
        var rental = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
            .FirstOrDefaultAsync(r => r.Id == rentalId, cancellationToken);

        if (rental == null) return null;

        var review = await _db.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId == rentalId, cancellationToken);

        if (review == null) return null;

        var reviewerName = Guid.TryParse(review.ReviewerUserId, out var rGuid)
            ? await GetUserDisplayNameAsync(rGuid, cancellationToken)
            : "Renter";

        return new MachineryReviewResponse(
            ReviewId: review.Id,
            RentalId: rental.Id,
            MachineryId: rental.MachineryId,
            MachineryName: rental.Machinery.Name,
            ReviewerName: reviewerName,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAtUtc: review.CreatedAtUtc,
            UpdatedAtUtc: review.UpdatedAtUtc
        );
    }

    public async Task<MachineryRatingSummaryResponse> GetMachineryReviewsAsync(Guid machineryId, CancellationToken cancellationToken = default)
    {
        var machinery = await _db.Machinery
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == machineryId, cancellationToken);

        if (machinery == null)
        {
            return new MachineryRatingSummaryResponse(0.0, 0, Array.Empty<MachineryReviewResponse>());
        }

        var rentalMap = await _db.MachineryRentals
            .AsNoTracking()
            .Where(r => r.MachineryId == machineryId)
            .Select(r => new { r.Id, r.MachineryId })
            .ToDictionaryAsync(r => r.Id, r => r.MachineryId, cancellationToken);

        if (rentalMap.Count == 0)
        {
            return new MachineryRatingSummaryResponse(0.0, 0, Array.Empty<MachineryReviewResponse>());
        }

        var rentalIds = rentalMap.Keys.ToList();

        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId.HasValue && rentalIds.Contains(r.RelatedEntityId.Value))
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            return new MachineryRatingSummaryResponse(0.0, 0, Array.Empty<MachineryReviewResponse>());
        }

        double avgRating = Math.Round(reviews.Average(r => r.Rating), 1);

        var reviewerGuids = reviews
            .Select(r => Guid.TryParse(r.ReviewerUserId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        var userNames = await GetUserDisplayNamesAsync(reviewerGuids, cancellationToken);

        var recentResponses = reviews.Select(r => new MachineryReviewResponse(
            ReviewId: r.Id,
            RentalId: r.RelatedEntityId!.Value,
            MachineryId: machineryId,
            MachineryName: machinery.Name,
            ReviewerName: userNames.TryGetValue(r.ReviewerUserId, out var name) ? name : "Renter",
            Rating: r.Rating,
            Comment: r.Comment,
            CreatedAtUtc: r.CreatedAtUtc,
            UpdatedAtUtc: r.UpdatedAtUtc
        )).ToList();

        return new MachineryRatingSummaryResponse(
            AverageRating: avgRating,
            TotalReviews: reviews.Count,
            RecentReviews: recentResponses
        );
    }

    public async Task<MachineryRatingSummaryResponse> GetOwnerMachineryReviewsAsync(string ownerUserId, Guid machineryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var machinery = await _db.Machinery
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == machineryId, cancellationToken);

        if (machinery == null)
        {
            throw new KeyNotFoundException("Machinery not found.");
        }

        if (!string.Equals(machinery.OwnerUserId, ownerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You are not authorized to access private reviews for this machinery.");
        }

        return await GetMachineryReviewsAsync(machineryId, cancellationToken);
    }

    public async Task<UserMyReviewsSummaryResponse> GetUnifiedMyReviewsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new UserMyReviewsSummaryResponse(0, 0, 0, Array.Empty<UnifiedReviewItemResponse>(), Array.Empty<UnifiedReviewItemResponse>(), Array.Empty<UnifiedReviewItemResponse>());
        }

        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerUserId == userId || r.RevieweeUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            return new UserMyReviewsSummaryResponse(0, 0, 0, Array.Empty<UnifiedReviewItemResponse>(), Array.Empty<UnifiedReviewItemResponse>(), Array.Empty<UnifiedReviewItemResponse>());
        }

        var orderReviewEntityIds = reviews
            .Where(r => r.RelatedEntityType == ReviewEntityType.Order && r.RelatedEntityId.HasValue)
            .Select(r => r.RelatedEntityId!.Value)
            .Distinct()
            .ToList();

        var rentalReviewEntityIds = reviews
            .Where(r => r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId.HasValue)
            .Select(r => r.RelatedEntityId!.Value)
            .Distinct()
            .ToList();

        var ordersMap = await _db.AuctionOrders
            .AsNoTracking()
            .Include(o => o.FarmerProfile)
            .Include(o => o.CustomerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Where(o => orderReviewEntityIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var rentalsMap = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
                .ThenInclude(m => m.Images)
            .Where(r => rentalReviewEntityIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var rentalUserGuids = rentalsMap.Values
            .SelectMany(r => new[] { r.RenterUserId, r.OwnerUserId })
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        var userDisplayNames = await GetUserDisplayNamesAsync(rentalUserGuids, cancellationToken);

        var unifiedList = new List<UnifiedReviewItemResponse>();

        foreach (var r in reviews)
        {
            if (r.RelatedEntityType == ReviewEntityType.Order && r.RelatedEntityId.HasValue && ordersMap.TryGetValue(r.RelatedEntityId.Value, out var order))
            {
                bool isWriter = string.Equals(r.ReviewerUserId, userId, StringComparison.OrdinalIgnoreCase);
                string targetName = isWriter ? order.FarmerProfile.FullName : order.CustomerProfile.FullName;
                string? img = order.Crop?.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? order.Crop?.Images?.FirstOrDefault()?.ImageUrl;

                unifiedList.Add(new UnifiedReviewItemResponse(
                    ReviewId: r.Id,
                    ReviewType: "CROP",
                    Rating: r.Rating,
                    Comment: r.Comment,
                    CreatedAtUtc: r.CreatedAtUtc,
                    UpdatedAtUtc: r.UpdatedAtUtc,
                    OrderId: order.Id,
                    OrderNumber: order.OrderNumber,
                    CropName: order.Crop?.CropName ?? "Crop",
                    CropType: order.Crop?.CropType,
                    RentalId: null,
                    RentalNumber: null,
                    MachineryId: null,
                    MachineryName: null,
                    TargetName: targetName,
                    PrimaryImageUrl: img,
                    CanEdit: isWriter
                ));
            }
            else if (r.RelatedEntityType == ReviewEntityType.MachineryRental && r.RelatedEntityId.HasValue && rentalsMap.TryGetValue(r.RelatedEntityId.Value, out var rental))
            {
                bool isWriter = string.Equals(r.ReviewerUserId, userId, StringComparison.OrdinalIgnoreCase);
                string counterpartyUserId = isWriter ? rental.OwnerUserId : rental.RenterUserId;
                string targetName = userDisplayNames.TryGetValue(counterpartyUserId, out var name) ? name : (isWriter ? "Owner" : "Renter");
                string? img = rental.Machinery?.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? rental.Machinery?.Images?.FirstOrDefault()?.ImageUrl;
                string rentalNum = "MR-" + rental.Id.ToString()[..6].ToUpper();

                unifiedList.Add(new UnifiedReviewItemResponse(
                    ReviewId: r.Id,
                    ReviewType: "MACHINERY",
                    Rating: r.Rating,
                    Comment: r.Comment,
                    CreatedAtUtc: r.CreatedAtUtc,
                    UpdatedAtUtc: r.UpdatedAtUtc,
                    OrderId: null,
                    OrderNumber: null,
                    CropName: null,
                    CropType: null,
                    RentalId: rental.Id,
                    RentalNumber: rentalNum,
                    MachineryId: rental.MachineryId,
                    MachineryName: rental.Machinery?.Name ?? "Machinery",
                    TargetName: targetName,
                    PrimaryImageUrl: img,
                    CanEdit: isWriter
                ));
            }
        }

        var cropReviews = unifiedList.Where(item => item.ReviewType == "CROP").ToList();
        var machineryReviews = unifiedList.Where(item => item.ReviewType == "MACHINERY").ToList();

        return new UserMyReviewsSummaryResponse(
            TotalCount: unifiedList.Count,
            CropCount: cropReviews.Count,
            MachineryCount: machineryReviews.Count,
            AllReviews: unifiedList,
            CropReviews: cropReviews,
            MachineryReviews: machineryReviews
        );
    }

    public async Task<MachineryReviewResponse> UpdateMachineryReviewAsync(string reviewerUserId, Guid reviewId, UpdateMachineryReviewRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRatingAndComment(request.Rating, request.Comment);

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.RelatedEntityType == ReviewEntityType.MachineryRental, cancellationToken);

        if (review == null)
        {
            throw new KeyNotFoundException("Machinery review not found.");
        }

        if (!string.Equals(review.ReviewerUserId, reviewerUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("You can only edit your own machinery review.");
        }

        review.Rating = request.Rating;
        review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        review.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var rental = await _db.MachineryRentals
            .AsNoTracking()
            .Include(r => r.Machinery)
            .FirstOrDefaultAsync(r => r.Id == review.RelatedEntityId!.Value, cancellationToken);

        var reviewerName = Guid.TryParse(reviewerUserId, out var rGuid)
            ? await GetUserDisplayNameAsync(rGuid, cancellationToken)
            : "Renter";

        return new MachineryReviewResponse(
            ReviewId: review.Id,
            RentalId: review.RelatedEntityId!.Value,
            MachineryId: rental?.MachineryId ?? Guid.Empty,
            MachineryName: rental?.Machinery?.Name ?? "Machinery",
            ReviewerName: reviewerName,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAtUtc: review.CreatedAtUtc,
            UpdatedAtUtc: review.UpdatedAtUtc
        );
    }

    private static void ValidateRatingAndComment(int rating, string? comment)
    {
        if (rating < 1 || rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5 stars.");
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            var trimmed = comment.Trim();
            if (trimmed.Length < 5 || trimmed.Length > 1000)
            {
                throw new ArgumentException("Review comment must be between 5 and 1000 characters.");
            }
        }
    }

    private async Task<string> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cust = await _db.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (cust != null) return cust.FullName;

        var farmer = await _db.FarmerProfiles.AsNoTracking().FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);
        if (farmer != null) return farmer.FullName;

        var worker = await _db.WorkerProfiles.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        if (worker != null) return worker.FullName;

        return "User";
    }

    private async Task<Dictionary<string, string>> GetUserDisplayNamesAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>();

        var custs = await _db.CustomerProfiles.AsNoTracking().Where(c => userIds.Contains(c.UserId)).ToListAsync(cancellationToken);
        foreach (var c in custs) result[c.UserId.ToString()] = c.FullName;

        var farmers = await _db.FarmerProfiles.AsNoTracking().Where(f => userIds.Contains(f.UserId)).ToListAsync(cancellationToken);
        foreach (var f in farmers) if (!result.ContainsKey(f.UserId.ToString())) result[f.UserId.ToString()] = f.FullName;

        return result;
    }
}
