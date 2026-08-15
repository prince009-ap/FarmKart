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
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly FarmKartDbContext _dbContext;

    public NotificationService(FarmKartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateNotificationAsync(
        string recipientUserId,
        string title,
        string message,
        NotificationType notificationType,
        Guid? relatedEntityId = null,
        Guid? relatedOrderId = null,
        Guid? relatedAuctionId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId)) return;

        var cleanRecipient = recipientUserId.Trim();
        var cleanTitle = title.Trim();
        var cleanMessage = message.Trim();

        // Idempotency / Duplicate check: prevent duplicate status update notifications for the same order
        if (relatedOrderId.HasValue)
        {
            var exists = await _dbContext.Notifications.AnyAsync(n =>
                n.RecipientUserId == cleanRecipient &&
                n.NotificationType == notificationType &&
                n.RelatedOrderId == relatedOrderId.Value,
                cancellationToken);

            if (exists)
            {
                return;
            }
        }

        var notification = new Notification
        {
            RecipientUserId = cleanRecipient,
            Title = cleanTitle,
            Message = cleanMessage,
            NotificationType = notificationType,
            IsRead = false,
            RelatedEntityId = relatedEntityId,
            RelatedOrderId = relatedOrderId,
            RelatedAuctionId = relatedAuctionId
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var recipientUserId = userId.ToString();
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return notifications.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<WorkerNotificationResponse>> GetWorkerNotificationsAsync(Guid userId)
    {
        var recipientUserId = userId.ToString();
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync();

        return notifications.Select(ToWorkerResponse).ToList();
    }

    public async Task<UnreadNotificationCountResponse> GetUnreadCountAsync(Guid userId)
    {
        var recipientUserId = userId.ToString();
        var count = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == recipientUserId && !n.IsRead);

        return new UnreadNotificationCountResponse(count);
    }

    public async Task<NotificationResponse> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var recipientUserId = userId.ToString();
        var notification = await _dbContext.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == recipientUserId);

        if (notification is null)
        {
            throw new KeyNotFoundException("Notification not found.");
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

    private static NotificationResponse ToResponse(Notification n) => new(
        Id: n.Id,
        Title: n.Title,
        Message: n.Message,
        NotificationType: n.NotificationType.ToString(),
        IsRead: n.IsRead,
        RelatedEntityId: n.RelatedEntityId,
        RelatedOrderId: n.RelatedOrderId,
        RelatedAuctionId: n.RelatedAuctionId,
        CreatedAtUtc: n.CreatedAtUtc
    );

    private static WorkerNotificationResponse ToWorkerResponse(Notification n) => new(
        Id: n.Id,
        Title: n.Title,
        Message: n.Message,
        NotificationType: n.NotificationType,
        IsRead: n.IsRead,
        RelatedEntityId: n.RelatedEntityId,
        CreatedAtUtc: n.CreatedAtUtc
    );
}
