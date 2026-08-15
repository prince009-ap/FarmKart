using FarmKart.Domain.Enums;
using System;

namespace FarmKart.Application.DTOs;

public record WorkerNotificationResponse(
    Guid Id,
    string Title,
    string Message,
    NotificationType NotificationType,
    bool IsRead,
    Guid? RelatedEntityId,
    DateTime CreatedAtUtc
);

public record NotificationResponse(
    Guid Id,
    string Title,
    string Message,
    string NotificationType,
    bool IsRead,
    Guid? RelatedEntityId,
    Guid? RelatedOrderId,
    Guid? RelatedAuctionId,
    DateTime CreatedAtUtc
);

public record UnreadNotificationCountResponse(
    int UnreadCount
);
