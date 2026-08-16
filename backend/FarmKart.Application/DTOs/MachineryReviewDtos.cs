using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record CreateMachineryReviewRequest(
    int Rating,
    string? Comment
);

public record UpdateMachineryReviewRequest(
    int Rating,
    string? Comment
);

public record MachineryReviewResponse(
    Guid ReviewId,
    Guid RentalId,
    Guid MachineryId,
    string MachineryName,
    string ReviewerName,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record MachineryRatingSummaryResponse(
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<MachineryReviewResponse> RecentReviews
);
