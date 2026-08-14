using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record CreateWorkerReviewRequest(
    int Rating,
    string? Comment = null
);

public record WorkerReviewResponse(
    Guid ReviewId,
    Guid WorkerAssignmentId,
    string FarmerName,
    string JobTitle,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc
);

public record WorkerRatingBreakdownResponse(
    int FiveStars,
    int FourStars,
    int ThreeStars,
    int TwoStars,
    int OneStar
);

public record WorkerRatingSummaryResponse(
    double AverageRating,
    int TotalReviews,
    WorkerRatingBreakdownResponse Breakdown,
    IReadOnlyList<WorkerReviewResponse> RecentReviews
);
