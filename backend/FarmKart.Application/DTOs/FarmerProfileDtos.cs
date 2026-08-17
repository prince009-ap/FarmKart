using System;
using System.Collections.Generic;

namespace FarmKart.Application.DTOs;

public record FarmerPublicReviewResponse(
    Guid ReviewId,
    string ReviewerName,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc
);

public record FarmerPublicAuctionResponse(
    Guid AuctionId,
    string Title,
    string CropName,
    string CropType,
    decimal StartingPrice,
    decimal TotalQuantity,
    string Unit,
    string Status,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    string? PrimaryImageUrl
);

public record FarmerPublicMachineryResponse(
    Guid MachineryId,
    string Name,
    string Category,
    string? Brand,
    string? Model,
    decimal DailyRent,
    bool DriverAvailable,
    string AvailabilityStatus,
    double AverageRating,
    int ReviewCount,
    string? PrimaryImageUrl,
    string? Location,
    string? City,
    string? State
);

public record FarmerPublicProfileResponse(
    Guid FarmerId,
    Guid UserId,
    string FullName,
    string? FarmName,
    string? Location,
    string? City,
    string? State,
    DateTime MemberSinceUtc,
    double AverageRating,
    int TotalReviews,
    IReadOnlyList<FarmerPublicReviewResponse> Reviews,
    IReadOnlyList<FarmerPublicAuctionResponse> ActiveAuctions,
    IReadOnlyList<FarmerPublicMachineryResponse> Machinery,
    string? ProfileImageUrl = null
);
