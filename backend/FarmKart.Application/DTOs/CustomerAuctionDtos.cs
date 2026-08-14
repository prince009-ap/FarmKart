namespace FarmKart.Application.DTOs;

public sealed record CustomerAuctionResponse(
    Guid Id,
    Guid CropId,
    string CropName,
    string CropType,
    string? Variety,
    decimal Quantity,
    string Unit,
    decimal QuantityKg,
    decimal StartingBidPrice,
    decimal CurrentHighestBid,
    decimal MinimumBidIncrement,
    string FarmerName,
    string FarmLocation,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    string Status,
    string? PrimaryImageUrl,
    IReadOnlyList<string> Images,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime ServerTimeUtc
);

public sealed record CustomerAuctionFilterRequest(
    string? Search = null,
    string? Category = null,
    string? Status = null,
    string? Location = null,
    string? SortBy = null
);
