using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class WishlistConfigurations : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(w => w.ItemType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(w => w.CreatedAtUtc).IsRequired();
        builder.Property(w => w.UpdatedAtUtc).IsRequired();

        // Enforce no duplicate wishlist entries for a user
        builder.HasIndex(w => new { w.UserId, w.ItemType, w.ItemId })
            .IsUnique()
            .HasDatabaseName("IX_WishlistItems_UserId_ItemType_ItemId");

        // Fast lookups by user
        builder.HasIndex(w => w.UserId)
            .HasDatabaseName("IX_WishlistItems_UserId");
    }
}
