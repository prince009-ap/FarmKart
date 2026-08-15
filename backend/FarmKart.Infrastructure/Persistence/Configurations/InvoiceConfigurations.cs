using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(60);

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        builder.HasIndex(i => i.AuctionOrderId)
            .IsUnique();

        builder.Property(i => i.SellerName).HasMaxLength(150);
        builder.Property(i => i.SellerPhone).HasMaxLength(30);
        builder.Property(i => i.SellerLocation).HasMaxLength(250);

        builder.Property(i => i.BuyerName).HasMaxLength(150);
        builder.Property(i => i.BuyerPhone).HasMaxLength(30);
        builder.Property(i => i.DeliveryOrPickupAddress).HasMaxLength(500);

        builder.Property(i => i.CropName).HasMaxLength(150);
        builder.Property(i => i.CropType).HasMaxLength(100);
        builder.Property(i => i.Variety).HasMaxLength(100);
        builder.Property(i => i.PrimaryImageUrl).HasMaxLength(1000);

        builder.Property(i => i.QuantityKg).HasPrecision(18, 2);
        builder.Property(i => i.QuantityMan).HasPrecision(18, 2);
        builder.Property(i => i.PricePerMan).HasPrecision(18, 2);
        builder.Property(i => i.SubtotalAmount).HasPrecision(18, 2);
        builder.Property(i => i.TaxAmount).HasPrecision(18, 2);
        builder.Property(i => i.TotalAmount).HasPrecision(18, 2);

        builder.Property(i => i.PaymentStatus).HasMaxLength(50);
        builder.Property(i => i.PaymentReference).HasMaxLength(100);
        builder.Property(i => i.FulfillmentMode).HasMaxLength(50);

        builder.HasOne(i => i.AuctionOrder)
            .WithMany()
            .HasForeignKey(i => i.AuctionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.CustomerProfile)
            .WithMany()
            .HasForeignKey(i => i.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.FarmerProfile)
            .WithMany()
            .HasForeignKey(i => i.FarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
