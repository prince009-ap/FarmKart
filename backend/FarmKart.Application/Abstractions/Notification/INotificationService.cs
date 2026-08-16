using FarmKart.Application.DTOs;
using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Notification;

public interface INotificationService
{
    Task CreateNotificationAsync(
        string recipientUserId,
        string title,
        string message,
        NotificationType notificationType,
        Guid? relatedEntityId = null,
        Guid? relatedOrderId = null,
        Guid? relatedAuctionId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationResponse>> GetNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkerNotificationResponse>> GetWorkerNotificationsAsync(Guid userId);
    Task<UnreadNotificationCountResponse> GetUnreadCountAsync(Guid userId);
    Task<NotificationResponse> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task MarkAllAsReadAsync(Guid userId);
    Task DeleteNotificationAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task ClearNotificationsAsync(Guid userId, CancellationToken cancellationToken = default);
}
