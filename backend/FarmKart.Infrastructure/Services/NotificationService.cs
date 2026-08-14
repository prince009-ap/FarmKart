using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly FarmKartDbContext _dbContext;

    public NotificationService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateNotificationAsync(string recipientUserId, string title, string message, NotificationType notificationType, Guid? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId)) return;

        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Title = title.Trim(),
            Message = message.Trim(),
            NotificationType = notificationType,
            IsRead = false,
            RelatedEntityId = relatedEntityId
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<WorkerNotificationResponse>> GetWorkerNotificationsAsync(Guid userId)
    {
        var recipientUserId = userId.ToString();
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync();

        return notifications.Select(ToResponse).ToList();
    }

    public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync(Guid userId)
    {
        var recipientUserId = userId.ToString();
        var count = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == recipientUserId && !n.IsRead);

        return new UnreadNotificationCountResponse(count);
    }

    public async Task<WorkerNotificationResponse> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var recipientUserId = userId.ToString();
        var notification = await _dbContext.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == recipientUserId);

        if (notification is null)
        {
            throw new JobNotFoundException("Notification not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();
        }

        return ToResponse(notification);
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var recipientUserId = userId.ToString();
        var unreadNotifications = await _dbContext.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && !n.IsRead)
            .ToListAsync();

        if (unreadNotifications.Count > 0)
        {
            foreach (var n in unreadNotifications)
            {
                n.IsRead = true;
            }
            await _dbContext.SaveChangesAsync();
        }
    }

    private static WorkerNotificationResponse ToResponse(Notification n) => new(
        Id: n.Id,
        Title: n.Title,
        Message: n.Message,
        NotificationType: n.NotificationType,
        IsRead: n.IsRead,
        RelatedEntityId: n.RelatedEntityId,
        CreatedAtUtc: n.CreatedAtUtc
    );
}
