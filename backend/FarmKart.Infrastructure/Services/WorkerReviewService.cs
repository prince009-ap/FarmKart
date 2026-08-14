using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Worker;
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

public sealed class WorkerReviewService : IWorkerReviewService
{
    private readonly FarmKartDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public WorkerReviewService(FarmKartDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<WorkerReviewResponse> RateWorkerAsync(Guid farmerUserId, Guid assignmentId, CreateWorkerReviewRequest request)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5 stars.");
        }

        if (request.Comment != null && request.Comment.Length > 2000)
        {
            throw new ArgumentException("Review text cannot exceed 2000 characters.");
        }

        var farmerProfile = await _dbContext.FarmerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.UserId == farmerUserId);

        if (farmerProfile is null)
        {
            throw new ProfileNotFoundException("Farmer profile not found.");
        }

        var assignment = await _dbContext.WorkerAssignments
            .Include(a => a.Job)
                .ThenInclude(j => j.FarmerProfile)
            .Include(a => a.WorkerProfile)
            .SingleOrDefaultAsync(a => a.Id == assignmentId);

        if (assignment is null || assignment.Job.FarmerProfileId != farmerProfile.Id)
        {
            throw new JobNotFoundException("Worker assignment not found for this farmer.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isJobFinished = assignment.Status == AssignmentStatus.Completed
            || assignment.Job.Status == JobStatus.Completed
            || (assignment.EndDate.HasValue && assignment.EndDate.Value <= today)
            || (assignment.Job.EndDate <= today);

        if (!isJobFinished)
        {
            throw new InvalidOperationException("Rating can only be submitted for completed or finished worker assignments.");
        }

        var existingReview = await _dbContext.Reviews
            .SingleOrDefaultAsync(r => r.RelatedEntityId == assignmentId
                && r.RelatedEntityType == ReviewEntityType.WorkerAssignment
                && r.ReviewerUserId == farmerUserId.ToString());

        Review review;
        if (existingReview != null)
        {
            existingReview.Rating = request.Rating;
            existingReview.Comment = request.Comment;
            existingReview.UpdatedAtUtc = DateTime.UtcNow;
            review = existingReview;
        }
        else
        {
            review = new Review
            {
                ReviewerUserId = farmerUserId.ToString(),
                RevieweeUserId = assignment.WorkerProfile.UserId.ToString(),
                Rating = request.Rating,
                Comment = request.Comment,
                RelatedEntityType = ReviewEntityType.WorkerAssignment,
                RelatedEntityId = assignmentId,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Reviews.Add(review);
        }

        await _dbContext.SaveChangesAsync();

        // Trigger in-app notification
        try
        {
            var farmerName = farmerProfile.FullName ?? farmerProfile.FarmName ?? "Farmer";
            await _notificationService.CreateNotificationAsync(
                assignment.WorkerProfile.UserId.ToString(),
                "New Review Received",
                $"{farmerName} rated your work {request.Rating} stars.",
                NotificationType.Review,
                review.Id
            );
        }
        catch { /* Side effect failure must not roll back review transaction */ }

        return new WorkerReviewResponse(
            ReviewId: review.Id,
            WorkerAssignmentId: assignmentId,
            FarmerName: farmerProfile.FullName ?? farmerProfile.FarmName ?? "Farmer",
            JobTitle: assignment.Job.Title,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAtUtc: review.CreatedAtUtc
        );
    }

    public async Task<WorkerReviewResponse?> GetAssignmentReviewAsync(Guid farmerUserId, Guid assignmentId)
    {
        var review = await _dbContext.Reviews
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.RelatedEntityId == assignmentId
                && r.RelatedEntityType == ReviewEntityType.WorkerAssignment
                && r.ReviewerUserId == farmerUserId.ToString());

        if (review is null) return null;

        var assignment = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.FarmerProfile)
            .SingleOrDefaultAsync(a => a.Id == assignmentId);

        var farmerName = assignment?.Job?.FarmerProfile?.FullName ?? assignment?.Job?.FarmerProfile?.FarmName ?? "Farmer";
        var jobTitle = assignment?.Job?.Title ?? "Job";

        return new WorkerReviewResponse(
            ReviewId: review.Id,
            WorkerAssignmentId: assignmentId,
            FarmerName: farmerName,
            JobTitle: jobTitle,
            Rating: review.Rating,
            Comment: review.Comment,
            CreatedAtUtc: review.CreatedAtUtc
        );
    }

    public async Task<WorkerRatingSummaryResponse> GetWorkerRatingSummaryAsync(Guid workerUserId)
    {
        var workerProfile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.UserId == workerUserId);

        if (workerProfile is null)
        {
            throw new ProfileNotFoundException("Worker profile not found.");
        }

        return await GetWorkerRatingSummaryByProfileIdAsync(workerProfile.Id);
    }

    public async Task<WorkerRatingSummaryResponse> GetWorkerRatingSummaryByProfileIdAsync(Guid workerProfileId)
    {
        var workerProfile = await _dbContext.WorkerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == workerProfileId);

        if (workerProfile is null)
        {
            throw new ProfileNotFoundException("Worker profile not found.");
        }

        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.RevieweeUserId == workerProfile.UserId.ToString()
                && r.RelatedEntityType == ReviewEntityType.WorkerAssignment)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync();

        if (reviews.Count == 0)
        {
            return new WorkerRatingSummaryResponse(
                AverageRating: 0.0,
                TotalReviews: 0,
                Breakdown: new WorkerRatingBreakdownResponse(0, 0, 0, 0, 0),
                RecentReviews: []
            );
        }

        var avg = Math.Round(reviews.Average(r => r.Rating), 1);
        var fiveStars = reviews.Count(r => r.Rating == 5);
        var fourStars = reviews.Count(r => r.Rating == 4);
        var threeStars = reviews.Count(r => r.Rating == 3);
        var twoStars = reviews.Count(r => r.Rating == 2);
        var oneStar = reviews.Count(r => r.Rating == 1);

        // Map recent reviews with farmer name and job title
        var assignmentIds = reviews.Select(r => r.RelatedEntityId).Where(id => id.HasValue).Select(id => id!.Value).ToList();
        var assignmentsMap = await _dbContext.WorkerAssignments
            .AsNoTracking()
            .Include(a => a.Job)
                .ThenInclude(j => j.FarmerProfile)
            .Where(a => assignmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var recentList = reviews.Select(r =>
        {
            var farmerName = "Farmer";
            var jobTitle = "Completed Job";
            if (r.RelatedEntityId.HasValue && assignmentsMap.TryGetValue(r.RelatedEntityId.Value, out var a))
            {
                farmerName = a.Job?.FarmerProfile?.FullName ?? a.Job?.FarmerProfile?.FarmName ?? "Farmer";
                jobTitle = a.Job?.Title ?? "Completed Job";
            }

            return new WorkerReviewResponse(
                ReviewId: r.Id,
                WorkerAssignmentId: r.RelatedEntityId ?? Guid.Empty,
                FarmerName: farmerName,
                JobTitle: jobTitle,
                Rating: r.Rating,
                Comment: r.Comment,
                CreatedAtUtc: r.CreatedAtUtc
            );
        }).ToList();

        return new WorkerRatingSummaryResponse(
            AverageRating: avg,
            TotalReviews: reviews.Count,
            Breakdown: new WorkerRatingBreakdownResponse(fiveStars, fourStars, threeStars, twoStars, oneStar),
            RecentReviews: recentList
        );
    }
}
