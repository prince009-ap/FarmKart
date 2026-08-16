using System.Security.Claims;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/customer/wishlist")]
[Authorize(Roles = Roles.Customer)]
public sealed class CustomerWishlistController(IWishlistService wishlistService) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>GET /api/customer/wishlist — retrieve all wishlist items, optionally filtered by itemType (1=Crop, 2=Auction).</summary>
    [HttpGet]
    public async Task<IActionResult> GetWishlist([FromQuery] WishlistItemType? itemType, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized();

        var items = await wishlistService.GetWishlistAsync(userId, itemType, cancellationToken);
        return Ok(items);
    }

    /// <summary>GET /api/customer/wishlist/count — retrieve wishlist counts.</summary>
    [HttpGet("count")]
    public async Task<IActionResult> GetCount(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized();

        var count = await wishlistService.GetCountAsync(userId, cancellationToken);
        return Ok(count);
    }

    /// <summary>GET /api/customer/wishlist/{itemType}/{itemId}/status — check if a specific item is in the wishlist.</summary>
    [HttpGet("{itemType}/{itemId:guid}/status")]
    public async Task<IActionResult> GetItemStatus(WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized();

        var status = await wishlistService.GetItemStatusAsync(userId, itemType, itemId, cancellationToken);
        return Ok(status);
    }

    /// <summary>POST /api/customer/wishlist — add an item (idempotent).</summary>
    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] AddWishlistItemRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (CurrentUserId is not { } userId)
            return Unauthorized();

        try
        {
            var item = await wishlistService.AddAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetItemStatus),
                new { itemType = request.ItemType, itemId = request.ItemId },
                item);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>DELETE /api/customer/wishlist/{itemType}/{itemId} — remove an item.</summary>
    [HttpDelete("{itemType}/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(WishlistItemType itemType, Guid itemId, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized();

        try
        {
            await wishlistService.RemoveAsync(userId, itemType, itemId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
