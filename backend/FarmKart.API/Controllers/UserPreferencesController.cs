using FarmKart.Application.Abstractions.UserPreference;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/preferences")]
[Authorize]
public sealed class UserPreferencesController(IUserPreferenceService preferenceService) : ControllerBase
{
    private readonly IUserPreferenceService _preferenceService = preferenceService;

    [HttpGet]
    public async Task<ActionResult<UserPreferenceResponse>> GetPreferences(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var result = await _preferenceService.GetUserPreferenceAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<UserPreferenceResponse>> UpdatePreferences([FromBody] UpdateUserPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var result = await _preferenceService.UpdateUserPreferenceAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("account")]
    public async Task<ActionResult<AccountSettingsResponse>> GetAccountSettings(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = GetRole();

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId) || string.IsNullOrEmpty(role))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _preferenceService.GetAccountSettingsAsync(userId, role, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("account")]
    public async Task<ActionResult<AccountSettingsResponse>> UpdateAccountProfile([FromBody] UpdateAccountProfileRequest request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = GetRole();

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId) || string.IsNullOrEmpty(role))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _preferenceService.UpdateAccountProfileAsync(userId, role, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _preferenceService.ChangePasswordAsync(userId, request, cancellationToken);
            return Ok(new { message = "Password changed successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private string? GetRole()
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        if (!string.IsNullOrEmpty(role)) return role;

        if (User.IsInRole(Roles.Farmer)) return Roles.Farmer;
        if (User.IsInRole(Roles.Worker)) return Roles.Worker;
        if (User.IsInRole(Roles.Customer)) return Roles.Customer;

        return null;
    }
}
