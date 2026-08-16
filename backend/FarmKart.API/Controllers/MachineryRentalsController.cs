using System.Security.Claims;
using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

/// <summary>
/// Machinery rental booking and lifecycle management.
/// Accessible by both Farmers and Customers.
/// </summary>
[ApiController]
[Authorize(Roles = $"{Roles.Farmer},{Roles.Customer}")]
public sealed class MachineryRentalsController(
    IMachineryRentalService rentalService) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>POST /api/machinery/{id}/rentals — Book a rental for a machinery.</summary>
    [HttpPost("/api/machinery/{id:guid}/rentals")]
    public async Task<IActionResult> BookRental(Guid id, [FromBody] BookRentalRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var result = await rentalService.BookRentalAsync(userId, id, request, cancellationToken);
            return CreatedAtAction(nameof(GetRentalById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>GET /api/my-rentals — Get all rentals where I am the renter.</summary>
    [HttpGet("/api/my-rentals")]
    public async Task<IActionResult> GetMyRentals(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await rentalService.GetMyRentalsAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/machinery-owner/rentals — Get all rentals for my listed machinery (as owner).</summary>
    [HttpGet("/api/machinery-owner/rentals")]
    public async Task<IActionResult> GetMyListingsRentals(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await rentalService.GetMyListingsRentalsAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/rentals/{id} — Get a single rental by ID (user must be owner or renter).</summary>
    [HttpGet("/api/rentals/{id:guid}")]
    public async Task<IActionResult> GetRentalById(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await rentalService.GetRentalByIdAsync(userId, id, cancellationToken);
        return result is null ? NotFound(new { message = "Rental not found." }) : Ok(result);
    }

    /// <summary>PATCH /api/rentals/{id}/status — Update rental status (state machine).</summary>
    [HttpPatch("/api/rentals/{id:guid}/status")]
    public async Task<IActionResult> UpdateRentalStatus(Guid id, [FromBody] UpdateRentalStatusRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var result = await rentalService.UpdateRentalStatusAsync(userId, id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
