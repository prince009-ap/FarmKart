using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Order_TotalAmount_NonNegative", "[TotalAmount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(order => order.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(order => order.TotalAmount).HasPrecision(18, 2);

        builder.HasIndex(order => order.CustomerProfileId);
        builder.HasIndex(order => order.OrderNumber).IsUnique();

        builder.HasOne(order => order.CustomerProfile)
            .WithMany(customer => customer.Orders)
            .HasForeignKey(order => order.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_OrderItem_Values_NonNegative", "[Quantity] >= 0 AND [UnitPrice] >= 0 AND [TotalPrice] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(item => item.Quantity).HasPrecision(18, 2);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.TotalPrice).HasPrecision(18, 2);

        builder.HasOne(item => item.Order)
            .WithMany(order => order.OrderItems)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.CropListing)
            .WithMany(listing => listing.OrderItems)
            .HasForeignKey(item => item.CropListingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Payment_Amount_NonNegative", "[Amount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.TransactionReference).HasMaxLength(150);

        builder.HasOne(payment => payment.Order)
            .WithMany(order => order.Payments)
            .HasForeignKey(payment => payment.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ConfigureBaseEntity();
        builder.ConfigureAddressInfo(delivery => delivery.AddressInfo);

        builder.HasOne(delivery => delivery.Order)
            .WithMany(order => order.Deliveries)
            .HasForeignKey(delivery => delivery.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
