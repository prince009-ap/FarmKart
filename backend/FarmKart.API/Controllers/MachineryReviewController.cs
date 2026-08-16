using FarmKart.Application.Abstractions.Machinery;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Authorize]
public sealed class MachineryReviewController : ControllerBase
{
    private readonly IMachineryReviewService _machineryReviewService;

    public MachineryReviewController(IMachineryReviewService machineryReviewService)
    {
        _machineryReviewService = machineryReviewService;
    }

    [HttpPost("api/rentals/{rentalId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(MachineryReviewResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRentalReview(Guid rentalId, [FromBody] CreateMachineryReviewRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userIdStr) return Unauthorized();

        try
        {
            var response = await _machineryReviewService.CreateMachineryReviewAsync(userIdStr, rentalId, request, cancellationToken);
            return CreatedAtAction(nameof(GetRentalReview), new { rentalId }, response);
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
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("api/rentals/{rentalId:guid}/review")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MachineryReviewResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRentalReview(Guid rentalId, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userIdStr) return Unauthorized();

        var review = await _machineryReviewService.GetRentalReviewAsync(userIdStr, rentalId, cancellationToken);
        if (review == null)
        {
            return NotFound(new { message = "No review found for this machinery rental." });
        }

        return Ok(review);
    }

    [HttpGet("api/machinery/{machineryId:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MachineryRatingSummaryResponse))]
    public async Task<IActionResult> GetMachineryReviews(Guid machineryId, CancellationToken cancellationToken)
    {
        var reviews = await _machineryReviewService.GetMachineryReviewsAsync(machineryId, cancellationToken);
        return Ok(reviews);
    }

    [HttpGet("api/my-machinery/{machineryId:guid}/reviews")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MachineryRatingSummaryResponse))]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnerMachineryReviews(Guid machineryId, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userIdStr) return Unauthorized();

        try
        {
            var summary = await _machineryReviewService.GetOwnerMachineryReviewsAsync(userIdStr, machineryId, cancellationToken);
            return Ok(summary);
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

    [HttpGet("api/my-reviews")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserMyReviewsSummaryResponse))]
    public async Task<IActionResult> GetUnifiedMyReviews(CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userIdStr) return Unauthorized();

        var summary = await _machineryReviewService.GetUnifiedMyReviewsAsync(userIdStr, cancellationToken);
        return Ok(summary);
    }

    [HttpPut("api/reviews/{reviewId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MachineryReviewResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(Guid reviewId, [FromBody] UpdateMachineryReviewRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userIdStr) return Unauthorized();

        try
        {
            var response = await _machineryReviewService.UpdateMachineryReviewAsync(userIdStr, reviewId, request, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
