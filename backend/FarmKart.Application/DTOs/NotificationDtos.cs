using FarmKart.Domain.Enums;
using System;
using System.Collections.Generic;

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
    DateTime? ReadAtUtc,
    string Priority,
    string? ActionUrl,
    Guid? RelatedEntityId,
    Guid? RelatedOrderId,
    Guid? RelatedAuctionId,
    DateTime CreatedAtUtc
);

public record UnreadNotificationCountResponse(
    int UnreadCount
);

public record NotificationQueryRequest(
    string? Filter = "all", // "all", "unread", "read"
    string? Category = null, // e.g. "Auction", "Order", "Payment", "Rental", "Review", "Dispute"
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);

public record PagedNotificationResponse(
    IReadOnlyList<NotificationResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    int UnreadCount
);
