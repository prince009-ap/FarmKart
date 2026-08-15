using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class Conversation : BaseEntity
{
    public string? Topic { get; set; }

    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}

public sealed class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ParticipantProfileType ProfileType { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string SenderUserId { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public sealed class Notification : BaseEntity
{
    public string RecipientUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; } = NotificationType.General;
    public bool IsRead { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public Guid? RelatedOrderId { get; set; }
    public Guid? RelatedAuctionId { get; set; }
}

public sealed class Review : BaseEntity
{
    public string ReviewerUserId { get; set; } = string.Empty;
    public string RevieweeUserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public ReviewEntityType RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
