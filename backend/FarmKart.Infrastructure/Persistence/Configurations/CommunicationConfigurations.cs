using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ConfigureBaseEntity();
        builder.Property(conversation => conversation.Topic).HasMaxLength(200);
    }
}

public sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(participant => participant.UserId).HasMaxLength(128).IsRequired();
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId }).IsUnique();

        builder.HasOne(participant => participant.Conversation)
            .WithMany(conversation => conversation.Participants)
            .HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(message => message.SenderUserId).HasMaxLength(128).IsRequired();
        builder.Property(message => message.MessageText).HasMaxLength(4000).IsRequired();

        builder.HasOne(message => message.Conversation)
            .WithMany(conversation => conversation.Messages)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(notification => notification.RecipientUserId).HasMaxLength(128).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(2000).IsRequired();

        builder.HasIndex(notification => notification.RecipientUserId);
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.IsRead });
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Review_Rating_Range", "[Rating] >= 1 AND [Rating] <= 5");
        });

        builder.ConfigureBaseEntity();

        builder.Property(review => review.ReviewerUserId).HasMaxLength(128).IsRequired();
        builder.Property(review => review.RevieweeUserId).HasMaxLength(128).IsRequired();
        builder.Property(review => review.Comment).HasMaxLength(2000);

        builder.HasIndex(review => review.ReviewerUserId);
        builder.HasIndex(review => review.RevieweeUserId);
        builder.HasIndex(review => new { review.RelatedEntityType, review.RelatedEntityId })
            .HasFilter("[RelatedEntityId] IS NOT NULL")
            .IsUnique();
    }
}
