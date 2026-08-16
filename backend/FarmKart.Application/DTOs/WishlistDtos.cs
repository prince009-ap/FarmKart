using System.ComponentModel.DataAnnotations;
using FarmKart.Domain.Enums;

namespace FarmKart.Application.DTOs;

public sealed record AddWishlistItemRequest(
    [Required]
    WishlistItemType ItemType,

    [Required]
    Guid ItemId
);

public sealed record WishlistItemResponse
{
    public Guid Id { get; init; }
    public WishlistItemType ItemType { get; init; }
    public Guid ItemId { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    // Enriched Crop fields
    public string? CropName { get; init; }
    public string? CropType { get; init; }
    public string? Variety { get; init; }
    public string? FarmerName { get; init; }
    public string? PrimaryImageUrl { get; init; }
    public string? CropStatus { get; init; }

    // Enriched Auction fields
    public string? AuctionStatus { get; init; }
    public decimal? StartingBidPrice { get; init; }
    public decimal? CurrentHighestBid { get; init; }
    public decimal? QuantityKg { get; init; }
    public decimal? QuantityMan { get; init; }
    public DateTime? AuctionStartTimeUtc { get; init; }
    public DateTime? AuctionEndTimeUtc { get; init; }
    public DateTime? ServerTimeUtc { get; init; }
    public bool IsAuctionExpired { get; init; }
    public bool IsItemAvailable { get; init; } = true;
}

public sealed record WishlistCountResponse
{
    public int Total { get; init; }
    public int CropCount { get; init; }
    public int AuctionCount { get; init; }
}

public sealed record WishlistStatusResponse
{
    public bool IsFavorited { get; init; }
    public Guid? WishlistItemId { get; init; }
}
