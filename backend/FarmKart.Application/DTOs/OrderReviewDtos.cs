using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record CreateOrderReviewRequest(
    int Rating,
    string? Comment
);

public record UpdateOrderReviewRequest(
    int Rating,
    string? Comment
);

public record OrderReviewResponse(
    Guid ReviewId,
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string FarmerName,
    string CropName,
    string? CropType,
    string? PrimaryImageUrl,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);

public record FarmerRatingSummaryResponse(
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<OrderReviewResponse> RecentReviews
);
