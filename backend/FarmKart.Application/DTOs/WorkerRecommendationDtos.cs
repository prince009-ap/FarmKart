using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

/// <summary>
/// Detailed DTO for a single worker recommendation item.
/// </summary>
public record WorkerRecommendationItemDto(
    Guid WorkerProfileId,
    Guid UserId,
    string FullName,
    string? ProfileImageUrl,
    int ExperienceYears,
    decimal ExpectedDailyWage,
    string? City,
    string? State,
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<string> Skills,
    bool HasApplied,
    double MatchScore,      // Normalized match score [0–100]
    double ConfidenceScore, // AI-blended confidence score [0.0–1.0]
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Top-level response for the Worker Recommendation API endpoint.
/// </summary>
public record WorkerRecommendationResponse(
    Guid JobId,
    string JobTitle,
    string WorkCategory,
    IReadOnlyList<WorkerRecommendationItemDto> Recommendations,
    bool AiEnriched,
    string? FallbackReason = null
);
