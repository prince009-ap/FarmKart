using System.Security.Claims;
using FarmKart.Application.Abstractions.Dispute;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public sealed class DisputesController(IDisputeService disputeService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateDispute([FromBody] CreateDisputeRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var dispute = await disputeService.CreateDisputeAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetDisputeById), new { id = dispute.Id }, dispute);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetUserDisputes([FromQuery] DisputeQueryRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await disputeService.GetUserDisputesAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDisputeById(Guid id, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var dispute = await disputeService.GetDisputeByIdAsync(userId, id, cancellationToken);
            if (dispute == null)
            {
                return NotFound(new { message = "Dispute not found." });
            }

            return Ok(dispute);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> CloseDispute(Guid id, [FromBody] CloseDisputeRequest? body, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var dispute = await disputeService.CloseDisputeAsync(userId, id, body?.ResolutionNote, cancellationToken);
            return Ok(dispute);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}

public record CloseDisputeRequest(string? ResolutionNote);
