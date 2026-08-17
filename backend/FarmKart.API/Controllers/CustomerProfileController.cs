using FarmKart.Application.Abstractions.Customer;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/customer/profile")]
[Authorize(Roles = Roles.Customer)]
public sealed class CustomerProfileController : ControllerBase
{
    private readonly ICustomerProfileService _customerProfileService;

    public CustomerProfileController(ICustomerProfileService customerProfileService)
    {
        _customerProfileService = customerProfileService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var profile = await _customerProfileService.GetProfileAsync(userId.Value, cancellationToken);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateCustomerProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var profile = await _customerProfileService.UpdateProfileAsync(userId.Value, request, cancellationToken);
            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadProfileImage(IFormFile file, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Uploaded file is empty." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var profile = await _customerProfileService.UploadProfileImageAsync(
                userId.Value, stream, file.FileName, file.ContentType, file.Length, cancellationToken);

            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("image")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerProfileResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveProfileImage(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var profile = await _customerProfileService.RemoveProfileImageAsync(userId.Value, cancellationToken);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdStr, out var userId))
        {
            return userId;
        }
        return null;
    }
}
