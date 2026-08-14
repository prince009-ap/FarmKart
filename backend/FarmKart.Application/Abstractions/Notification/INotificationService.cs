using FarmKart.Application.DTOs;
using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmKart.Application.Abstractions.Notification;

public interface INotificationService
{
    Task CreateNotificationAsync(string recipientUserId, string title, string message, NotificationType notificationType, Guid? relatedEntityId = null);
    Task<IReadOnlyList<WorkerNotificationResponse>> GetWorkerNotificationsAsync(Guid userId);
    Task<UnreadNotificationCountResponse> GetUnreadCountAsync(Guid userId);
    Task<WorkerNotificationResponse> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task MarkAllAsReadAsync(Guid userId);
}
