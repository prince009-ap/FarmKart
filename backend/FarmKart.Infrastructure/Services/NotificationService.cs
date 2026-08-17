using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.DTOs;
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
        string priority = "Normal",
        string? actionUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId)) return;

        var cleanRecipient = recipientUserId.Trim();
        var cleanTitle = title.Trim();
        var cleanMessage = message.Trim();

        // Idempotency / Duplicate check: prevent duplicate status update notifications for the same order or entity
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
        else if (relatedEntityId.HasValue && (notificationType == NotificationType.ReportDispute || notificationType == NotificationType.Auction))
        {
            var exists = await _dbContext.Notifications.AnyAsync(n =>
                n.RecipientUserId == cleanRecipient &&
                n.NotificationType == notificationType &&
                n.RelatedEntityId == relatedEntityId.Value &&
                n.Title == cleanTitle,
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
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority.Trim(),
            ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim(),
            RelatedEntityId = relatedEntityId,
            RelatedOrderId = relatedOrderId,
            RelatedAuctionId = relatedAuctionId
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedNotificationResponse> GetPagedNotificationsAsync(Guid userId, NotificationQueryRequest request, CancellationToken cancellationToken = default)
    {
        var recipientUserId = userId.ToString();
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId);

        // Filter: all, unread, read
        var filter = (request.Filter ?? "all").ToLowerInvariant().Trim();
        if (filter == "unread")
        {
            query = query.Where(n => !n.IsRead);
        }
        else if (filter == "read")
        {
            query = query.Where(n => n.IsRead);
        }

        // Category Filter
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var cat = request.Category.Trim().ToLowerInvariant();
            query = cat switch
            {
                "auction" => query.Where(n => n.NotificationType == NotificationType.Auction || n.RelatedAuctionId != null),
                "order" => query.Where(n => n.NotificationType == NotificationType.Order ||
                                            n.NotificationType == NotificationType.OrderCreated ||
                                            n.NotificationType == NotificationType.OrderConfirmed ||
                                            n.NotificationType == NotificationType.OrderReadyForPickup ||
                                            n.NotificationType == NotificationType.OrderPickedUp ||
                                            n.NotificationType == NotificationType.OrderDispatched ||
                                            n.NotificationType == NotificationType.OrderDelivered ||
                                            n.NotificationType == NotificationType.OrderCompleted ||
                                            n.NotificationType == NotificationType.AuctionOrderCreated ||
                                            n.RelatedOrderId != null),
                "payment" => query.Where(n => n.NotificationType == NotificationType.Payment || n.NotificationType == NotificationType.SettlementCompleted),
                "rental" => query.Where(n => n.NotificationType == NotificationType.MachineryRental || n.NotificationType == NotificationType.DriverRequested),
                "review" => query.Where(n => n.NotificationType == NotificationType.Review || n.NotificationType == NotificationType.ReviewReceived),
                "dispute" or "report" or "reportdispute" => query.Where(n => n.NotificationType == NotificationType.ReportDispute),
                "system" => query.Where(n => n.NotificationType == NotificationType.General),
                _ => query
            };
        }

        // Text Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(search) || n.Message.ToLower().Contains(search));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        int unreadCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == recipientUserId && !n.IsRead, cancellationToken);

        int pageSize = Math.Max(1, Math.Min(100, request.PageSize));
        int page = Math.Max(1, request.Page);
        int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = notifications.Select(ToResponse).ToList();

        return new PagedNotificationResponse(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages,
            UnreadCount: unreadCount
        );
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
            notification.ReadAtUtc = DateTime.UtcNow;
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
            var now = DateTime.UtcNow;
            foreach (var n in unreadNotifications)
            {
                n.IsRead = true;
                n.ReadAtUtc = now;
            }
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteNotificationAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var recipientUserId = userId.ToString();
        var notification = await _dbContext.Notifications
            .SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == recipientUserId, cancellationToken);

        if (notification != null)
        {
            _dbContext.Notifications.Remove(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ClearNotificationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var recipientUserId = userId.ToString();
        var notifications = await _dbContext.Notifications
            .Where(n => n.RecipientUserId == recipientUserId)
            .ToListAsync(cancellationToken);

        if (notifications.Count > 0)
        {
            _dbContext.Notifications.RemoveRange(notifications);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static NotificationResponse ToResponse(Notification n) => new(
        Id: n.Id,
        Title: n.Title,
        Message: n.Message,
        NotificationType: n.NotificationType.ToString(),
        IsRead: n.IsRead,
        ReadAtUtc: n.ReadAtUtc,
        Priority: n.Priority ?? "Normal",
        ActionUrl: n.ActionUrl,
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
