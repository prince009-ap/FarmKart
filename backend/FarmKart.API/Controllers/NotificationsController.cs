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
    public async Task<IActionResult> GetNotifications([FromQuery] NotificationQueryRequest request, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await notificationService.GetPagedNotificationsAsync(userId, request, cancellationToken);
        return Ok(result);
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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id, CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await notificationService.DeleteNotificationAsync(userId, id, cancellationToken);
        return Ok(new { message = "Notification deleted." });
    }

    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAllNotifications(CancellationToken cancellationToken)
    {
        if (GetCurrentUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await notificationService.ClearNotificationsAsync(userId, cancellationToken);
        return Ok(new { message = "All notifications cleared." });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : null;
    }
}
