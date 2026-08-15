using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public sealed class OrderReviewService : IOrderReviewService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public OrderReviewService(FarmKartDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<OrderReviewResponse> CreateOrderReviewAsync(string customerUserId, Guid orderId, CreateOrderReviewRequest request)
    {
        ValidateRatingAndComment(request.Rating, request.Comment);

        if (!Guid.TryParse(customerUserId, out var custGuid))
        {
            throw new UnauthorizedAccessException("Invalid customer user ID.");
        }

        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == custGuid);

        if (customerProfile is null)
        {
            throw new ProfileNotFoundException("Customer profile not found.");
        }

        var order = await _dbContext.AuctionOrders
            .Include(o => o.AuctionPayment)
            .Include(o => o.FarmerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Include(o => o.AuctionAllocation)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
        {
            throw new InvalidOperationException("Order not found.");
        }

        if (order.CustomerProfileId != customerProfile.Id)
        {
            throw new InvalidOperationException("You can only review your own orders.");
        }

        if (order.AuctionPayment is null || order.AuctionPayment.PaymentStatus != PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Order must be paid before reviewing.");
        }

        if (order.Status != OrderStatus.Completed)
        {
            throw new InvalidOperationException("Only completed orders can be reviewed.");
        }

        var existingReview = await _dbContext.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RelatedEntityType == ReviewEntityType.Order && r.RelatedEntityId == orderId);

        if (existingReview is not null)
        {
            throw new InvalidOperationException("A review has already been submitted for this order.");
        }

        var farmerUserIdStr = order.FarmerProfile.UserId.ToString();
        var crop = order.Crop;

        var review = new Review
        {
            ReviewerUserId = customerUserId,
            RevieweeUserId = farmerUserIdStr,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            RelatedEntityType = ReviewEntityType.Order,
            RelatedEntityId = orderId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        // Idempotently notify the farmer using INotificationService
        try
        {
            await _notificationService.CreateNotificationAsync(
                recipientUserId: farmerUserIdStr,
                title: "New Review Received",
                message: $"Customer {customerProfile.FullName} left a {request.Rating}-star review for completed order #{order.OrderNumber}.",
                notificationType: NotificationType.Review,
                relatedEntityId: review.Id,
                relatedOrderId: order.Id,
                relatedAuctionId: order.AuctionAllocation.AuctionId
            );
        }
        catch
        {
            // Safeguard against notification failure breaking review creation
        }

        return MapToResponse(review, order, customerProfile.FullName, order.FarmerProfile.FullName, crop);
    }

    public async Task<OrderReviewResponse?> GetOrderReviewForCustomerAsync(string customerUserId, Guid orderId)
    {
        if (!Guid.TryParse(customerUserId, out var custGuid))
        {
            throw new UnauthorizedAccessException("Invalid customer user ID.");
        }

        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == custGuid);

        if (customerProfile is null)
        {
            throw new ProfileNotFoundException("Customer profile not found.");
        }

        var order = await _dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.FarmerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.CustomerProfileId != customerProfile.Id)
        {
            return null;
        }

        var review = await _dbContext.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RelatedEntityType == ReviewEntityType.Order && r.RelatedEntityId == orderId);

        if (review is null)
        {
            return null;
        }

        return MapToResponse(review, order, customerProfile.FullName, order.FarmerProfile.FullName, order.Crop);
    }

    public async Task<OrderReviewResponse> UpdateOrderReviewAsync(string customerUserId, Guid orderId, UpdateOrderReviewRequest request)
    {
        ValidateRatingAndComment(request.Rating, request.Comment);

        if (!Guid.TryParse(customerUserId, out var custGuid))
        {
            throw new UnauthorizedAccessException("Invalid customer user ID.");
        }

        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == custGuid);

        if (customerProfile is null)
        {
            throw new ProfileNotFoundException("Customer profile not found.");
        }

        var order = await _dbContext.AuctionOrders
            .Include(o => o.FarmerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.CustomerProfileId != customerProfile.Id)
        {
            throw new InvalidOperationException("You can only edit reviews for your own orders.");
        }

        var review = await _dbContext.Reviews
            .FirstOrDefaultAsync(r => r.RelatedEntityType == ReviewEntityType.Order && r.RelatedEntityId == orderId);

        if (review is null)
        {
            throw new InvalidOperationException("No existing review found to update.");
        }

        if (review.ReviewerUserId != customerUserId)
        {
            throw new InvalidOperationException("You cannot edit another user's review.");
        }

        review.Rating = request.Rating;
        review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        review.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return MapToResponse(review, order, customerProfile.FullName, order.FarmerProfile.FullName, order.Crop);
    }

    public async Task<IReadOnlyList<OrderReviewResponse>> GetCustomerReviewsAsync(string customerUserId)
    {
        if (!Guid.TryParse(customerUserId, out var custGuid))
        {
            return Array.Empty<OrderReviewResponse>();
        }

        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == custGuid);

        if (customerProfile is null)
        {
            return Array.Empty<OrderReviewResponse>();
        }

        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.ReviewerUserId == customerUserId && r.RelatedEntityType == ReviewEntityType.Order)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        if (!reviews.Any())
        {
            return Array.Empty<OrderReviewResponse>();
        }

        var orderIds = reviews.Select(r => r.RelatedEntityId!.Value).Distinct().ToList();

        var orders = await _dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.FarmerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id);

        var result = new List<OrderReviewResponse>();

        foreach (var r in reviews)
        {
            if (r.RelatedEntityId.HasValue && orders.TryGetValue(r.RelatedEntityId.Value, out var order))
            {
                result.Add(MapToResponse(r, order, customerProfile.FullName, order.FarmerProfile.FullName, order.Crop));
            }
        }

        return result;
    }

    public async Task<OrderReviewResponse?> GetOrderReviewForFarmerAsync(string farmerUserId, Guid orderId)
    {
        if (!Guid.TryParse(farmerUserId, out var farmerGuid))
        {
            return null;
        }

        var farmerProfile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerGuid);

        if (farmerProfile is null)
        {
            return null;
        }

        var order = await _dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.CustomerProfile)
            .Include(o => o.FarmerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null || order.FarmerProfileId != farmerProfile.Id)
        {
            return null;
        }

        var review = await _dbContext.Reviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RelatedEntityType == ReviewEntityType.Order && r.RelatedEntityId == orderId);

        if (review is null)
        {
            return null;
        }

        return MapToResponse(review, order, order.CustomerProfile.FullName, farmerProfile.FullName, order.Crop);
    }

    public async Task<FarmerRatingSummaryResponse> GetFarmerRatingSummaryAsync(string farmerUserId)
    {
        if (!Guid.TryParse(farmerUserId, out var farmerGuid))
        {
            return new FarmerRatingSummaryResponse(0.0, 0, Array.Empty<OrderReviewResponse>());
        }

        var farmerProfile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == farmerGuid);

        if (farmerProfile is null)
        {
            return new FarmerRatingSummaryResponse(0.0, 0, Array.Empty<OrderReviewResponse>());
        }

        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.RevieweeUserId == farmerUserId && r.RelatedEntityType == ReviewEntityType.Order)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        if (!reviews.Any())
        {
            return new FarmerRatingSummaryResponse(0.0, 0, Array.Empty<OrderReviewResponse>());
        }

        var avgRating = Math.Round(reviews.Average(r => r.Rating), 1);
        var totalCount = reviews.Count;

        var orderIds = reviews.Select(r => r.RelatedEntityId!.Value).Distinct().ToList();

        var orders = await _dbContext.AuctionOrders
            .AsNoTracking()
            .Include(o => o.CustomerProfile)
            .Include(o => o.FarmerProfile)
            .Include(o => o.Crop)
                .ThenInclude(c => c.Images)
            .Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id);

        var recentResponses = new List<OrderReviewResponse>();

        foreach (var r in reviews)
        {
            if (r.RelatedEntityId.HasValue && orders.TryGetValue(r.RelatedEntityId.Value, out var order))
            {
                recentResponses.Add(MapToResponse(r, order, order.CustomerProfile.FullName, farmerProfile.FullName, order.Crop));
            }
        }

        return new FarmerRatingSummaryResponse(avgRating, totalCount, recentResponses);
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

    private static OrderReviewResponse MapToResponse(Review review, AuctionOrder order, string customerName, string farmerName, Crop crop)
    {
        var primaryImage = crop.Images?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? crop.Images?.FirstOrDefault()?.ImageUrl;

        return new OrderReviewResponse(
            ReviewId: review.Id,
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            CustomerName: customerName,
            FarmerName: farmerName,
            CropName: crop.CropName,
            CropType: crop.CropType,
            PrimaryImageUrl: primaryImage,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAtUtc: review.CreatedAtUtc,
            UpdatedAtUtc: review.UpdatedAtUtc
        );
    }
}
