using System.Security.Claims;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmKart.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var notifications = await notificationService.GetNotificationsAsync(userId, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var count = await notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    [HttpPatch("{id:guid}/read")]
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        try
        {
            var notification = await notificationService.MarkAsReadAsync(userId, id);
            return Ok(notification);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPatch("read-all")]
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await notificationService.MarkAllAsReadAsync(userId);
        return Ok(new { message = "All notifications marked as read." });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
