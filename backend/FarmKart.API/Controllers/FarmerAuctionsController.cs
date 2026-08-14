using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/farmer/auctions")]
[Authorize(Roles = Roles.Farmer)]
public sealed class FarmerAuctionsController(IFarmerAuctionService auctionService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => CurrentUserId() is { } userId ? Ok(await auctionService.GetAuctionsAsync(userId)) : Unauthorized();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try { return Ok(await auctionService.GetAuctionAsync(userId, id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFarmerAuctionRequest request)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try { var auction = await auctionService.CreateAuctionAsync(userId, request); return CreatedAtAction(nameof(Get), new { id = auction.Id }, auction); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateFarmerAuctionRequest request)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try { return Ok(await auctionService.UpdateAuctionAsync(userId, id, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (CurrentUserId() is not { } userId) return Unauthorized();
        try { await auctionService.CancelAuctionAsync(userId, id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
