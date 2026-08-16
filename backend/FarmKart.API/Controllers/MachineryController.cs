using System.Security.Claims;
using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

/// <summary>
/// Public machinery browse and owner machinery management.
/// Accessible by both Farmers and Customers.
/// </summary>
[ApiController]
[Authorize(Roles = $"{Roles.Farmer},{Roles.Customer}")]
public sealed class MachineryController(
    IMachineryService machineryService) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ─── Public Browse ─────────────────────────────────────────────────────

    /// <summary>GET /api/machinery — Browse all available machinery with filters.</summary>
    [HttpGet("/api/machinery")]
    public async Task<IActionResult> GetMachinery([FromQuery] MachineryFilterRequest filter, CancellationToken cancellationToken)
    {
        var result = await machineryService.GetMachineryAsync(filter, CurrentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/machinery/{id} — Get a single machinery by ID.</summary>
    [HttpGet("/api/machinery/{id:guid}")]
    public async Task<IActionResult> GetMachineryById(Guid id, CancellationToken cancellationToken)
    {
        var result = await machineryService.GetMachineryByIdAsync(id, CurrentUserId, cancellationToken);
        return result is null ? NotFound(new { message = "Machinery not found." }) : Ok(result);
    }

    /// <summary>GET /api/machinery/{id}/availability — Get booked date ranges.</summary>
    [HttpGet("/api/machinery/{id:guid}/availability")]
    public async Task<IActionResult> GetAvailability(Guid id, CancellationToken cancellationToken)
    {
        var result = await machineryService.GetAvailabilityAsync(id, cancellationToken);
        return Ok(result);
    }

    // ─── My Machinery (Owner CRUD) ─────────────────────────────────────────

    /// <summary>GET /api/my-machinery — Get all machinery owned by the current user.</summary>
    [HttpGet("/api/my-machinery")]
    public async Task<IActionResult> GetMyMachinery(CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await machineryService.GetMyMachineryAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/my-machinery — Create a new machinery listing.</summary>
    [HttpPost("/api/my-machinery")]
    public async Task<IActionResult> CreateMachinery([FromBody] CreateMachineryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var result = await machineryService.CreateMachineryAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetMachineryById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>PUT /api/my-machinery/{id} — Update an owned machinery listing.</summary>
    [HttpPut("/api/my-machinery/{id:guid}")]
    public async Task<IActionResult> UpdateMachinery(Guid id, [FromBody] UpdateMachineryRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var result = await machineryService.UpdateMachineryAsync(userId, id, request, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>DELETE /api/my-machinery/{id} — Soft-delete an owned machinery listing.</summary>
    [HttpDelete("/api/my-machinery/{id:guid}")]
    public async Task<IActionResult> DeleteMachinery(Guid id, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var deleted = await machineryService.DeleteMachineryAsync(userId, id, cancellationToken);
        return deleted ? NoContent() : NotFound(new { message = "Machinery not found." });
    }

    // ─── Machinery Images ──────────────────────────────────────────────────

    /// <summary>POST /api/my-machinery/{id}/images — Upload an image.</summary>
    [HttpPost("/api/my-machinery/{id:guid}/images")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, [FromForm] bool isPrimary = false, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { message = "Uploaded file is empty." });

        try
        {
            using var stream = file.OpenReadStream();
            var result = await machineryService.UploadMachineryImageAsync(
                userId, id, stream, file.FileName, file.ContentType, file.Length, isPrimary, cancellationToken);
            return Created($"/api/my-machinery/{id}/images/{result.Id}", result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>DELETE /api/my-machinery/{id}/images/{imageId} — Delete an image.</summary>
    [HttpDelete("/api/my-machinery/{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var deleted = await machineryService.DeleteMachineryImageAsync(userId, id, imageId, cancellationToken);
            return deleted ? NoContent() : NotFound(new { message = "Image not found." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>PUT /api/my-machinery/{id}/images/{imageId}/primary — Set an image as primary.</summary>
    [HttpPut("/api/my-machinery/{id:guid}/images/{imageId:guid}/primary")]
    public async Task<IActionResult> SetPrimaryImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var result = await machineryService.SetPrimaryMachineryImageAsync(userId, id, imageId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
