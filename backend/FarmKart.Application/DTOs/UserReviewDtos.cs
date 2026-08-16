using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record UnifiedReviewItemResponse(
    Guid ReviewId,
    string ReviewType, // "CROP" or "MACHINERY"
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    // Context details
    Guid? OrderId,
    string? OrderNumber,
    string? CropName,
    string? CropType,
    Guid? RentalId,
    string? RentalNumber,
    Guid? MachineryId,
    string? MachineryName,
    string? TargetName,
    string? PrimaryImageUrl,
    bool CanEdit
);

public record UserMyReviewsSummaryResponse(
    int TotalCount,
    int CropCount,
    int MachineryCount,
    IReadOnlyList<UnifiedReviewItemResponse> AllReviews,
    IReadOnlyList<UnifiedReviewItemResponse> CropReviews,
    IReadOnlyList<UnifiedReviewItemResponse> MachineryReviews
);
