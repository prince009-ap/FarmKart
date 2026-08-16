using FarmKart.Application.DTOs;
using FarmKart.Domain.Enums;

namespace FarmKart.Application.Abstractions.Customer;

public interface IWishlistService
{
    /// <summary>Add an item to the authenticated customer's wishlist. Returns the created entry (idempotent — returns existing if duplicate).</summary>
    Task<WishlistItemResponse> AddAsync(string userId, AddWishlistItemRequest request, CancellationToken cancellationToken = default);

    /// <summary>Remove an item from the authenticated customer's wishlist.</summary>
    Task RemoveAsync(string userId, WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Get all wishlist items for the authenticated customer, optionally filtered by type.</summary>
    Task<IReadOnlyList<WishlistItemResponse>> GetWishlistAsync(string userId, WishlistItemType? itemType = null, CancellationToken cancellationToken = default);

    /// <summary>Get wishlist counts (total, crop, auction) for the authenticated customer.</summary>
    Task<WishlistCountResponse> GetCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Check if a specific item is in the authenticated customer's wishlist.</summary>
    Task<WishlistStatusResponse> GetItemStatusAsync(string userId, WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Get the set of favorited itemIds for the current user and a given item type (for bulk state injection into lists).</summary>
    Task<HashSet<Guid>> GetFavoritedItemIdsAsync(string userId, WishlistItemType itemType, CancellationToken cancellationToken = default);
}
