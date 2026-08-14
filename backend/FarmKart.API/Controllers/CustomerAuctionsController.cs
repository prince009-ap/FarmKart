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
}
