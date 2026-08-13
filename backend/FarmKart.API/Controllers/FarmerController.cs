using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/farmer")]
[Authorize(Roles = Roles.Farmer)]
public class FarmerController : ControllerBase
{
    private readonly IFarmerProfileService _farmerProfileService;
    private readonly IFarmerJobService _farmerJobService;

    public FarmerController(
        IFarmerProfileService farmerProfileService,
        IFarmerJobService farmerJobService)
    {
        _farmerProfileService = farmerProfileService;
        _farmerJobService = farmerJobService;
    }

    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FarmerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var profile = await _farmerProfileService.GetProfileAsync(userId.Value);
            return Ok(profile);
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FarmerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] FarmerProfileUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // Validate FarmSizeUnit if provided
        if (request.FarmSizeUnit.HasValue &&
            !Enum.IsDefined(typeof(Domain.Enums.FarmSizeUnit), request.FarmSizeUnit.Value))
        {
            return BadRequest(new { message = "FarmSizeUnit must be a valid value." });
        }

        try
        {
            var profile = await _farmerProfileService.UpdateProfileAsync(userId.Value, request);
            return Ok(profile);
        }
        catch (ProfileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs()
    {
        var userId = GetCurrentUserId();
        return userId is null ? Unauthorized() : Ok(await _farmerJobService.GetJobsAsync(userId.Value));
    }

    [HttpGet("jobs/{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _farmerJobService.GetJobAsync(userId.Value, id)); }
        catch (JobNotFoundException) { return NotFound(new { message = "Job not found." }); }
    }

    [HttpPost("jobs")]
    public async Task<IActionResult> CreateJob([FromBody] CreateFarmerJobRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var job = await _farmerJobService.CreateJobAsync(userId.Value, request);
            return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job);
        }
        catch (ProfileNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("jobs/{id:guid}")]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] UpdateFarmerJobRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try { return Ok(await _farmerJobService.UpdateJobAsync(userId.Value, id, request)); }
        catch (JobNotFoundException) { return NotFound(new { message = "Job not found." }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("jobs/{id:guid}")]
    public async Task<IActionResult> CancelJob(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        try { await _farmerJobService.CancelJobAsync(userId.Value, id); return NoContent(); }
        catch (JobNotFoundException) { return NotFound(new { message = "Job not found." }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>
    /// Reads the current user's ID exclusively from authenticated JWT claims.
    /// The client never supplies the UserId.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }
}
