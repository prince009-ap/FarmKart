using System.Security.Claims;
using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/customer/auctions")]
[Authorize(Roles = Roles.Customer)]
public sealed class CustomerAuctionsController(ICustomerAuctionService customerAuctionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAuctions(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] string? location,
        [FromQuery] string? sortBy,
        CancellationToken cancellationToken)
    {
        var filter = new CustomerAuctionFilterRequest(search, category, status, location, sortBy);
        var auctions = await customerAuctionService.GetMarketplaceAuctionsAsync(filter, cancellationToken);
        return Ok(auctions);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAuctionById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var auction = await customerAuctionService.GetAuctionByIdAsync(id, cancellationToken);
            return Ok(auction);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/bids")]
    public async Task<IActionResult> PlaceBid(Guid id, [FromBody] PlaceBidRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var bid = await customerAuctionService.PlaceBidAsync(userId, id, request, cancellationToken);
            return CreatedAtAction(nameof(GetAuctionBids), new { id }, bid);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/bids")]
    public async Task<IActionResult> GetAuctionBids(Guid id, CancellationToken cancellationToken)
    {
        var bids = await customerAuctionService.GetAuctionBidsAsync(id, cancellationToken);
        return Ok(bids);
    }

    [HttpGet("~/api/customer/bids")]
    public async Task<IActionResult> GetMyBids(CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var bids = await customerAuctionService.GetCustomerBidsAsync(userId, cancellationToken);
            return Ok(bids);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
