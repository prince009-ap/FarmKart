using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class MachineryConfiguration : IEntityTypeConfiguration<Machinery>
{
    public void Configure(EntityTypeBuilder<Machinery> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Machinery_DailyRent_NonNegative", "[DailyRent] >= 0 AND [SecurityDeposit] >= 0 AND [DriverChargePerDay] >= 0");
            table.HasCheckConstraint("CK_Machinery_ManufacturingYear_Range", "[ManufacturingYear] IS NULL OR ([ManufacturingYear] >= 1900 AND [ManufacturingYear] <= 2100)");
        });

        builder.ConfigureBaseEntity();

        builder.Property(m => m.OwnerUserId).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Category).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Brand).HasMaxLength(100);
        builder.Property(m => m.Model).HasMaxLength(100);
        builder.Property(m => m.Description).HasMaxLength(2000);
        builder.Property(m => m.DailyRent).HasPrecision(18, 2);
        builder.Property(m => m.SecurityDeposit).HasPrecision(18, 2);
        builder.Property(m => m.DriverChargePerDay).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(m => m.DriverName).HasMaxLength(150);
        builder.Property(m => m.DriverPhone).HasMaxLength(50);
        builder.Property(m => m.DriverNotes).HasMaxLength(1000);
        builder.Property(m => m.Location).HasMaxLength(250).IsRequired();
        builder.Property(m => m.City).HasMaxLength(100);
        builder.Property(m => m.State).HasMaxLength(100);
        builder.Property(m => m.Pincode).HasMaxLength(12);
        builder.Property(m => m.IsActive).HasDefaultValue(true);

        builder.HasIndex(m => m.OwnerUserId);
        builder.HasIndex(m => m.Category);
        builder.HasIndex(m => m.IsActive);
        builder.HasIndex(m => m.DriverAvailable);
    }
}

public sealed class MachineryImageConfiguration : IEntityTypeConfiguration<MachineryImage>
{
    public void Configure(EntityTypeBuilder<MachineryImage> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(i => i.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(i => i.IsPrimary).HasDefaultValue(false);

        builder.HasIndex(i => new { i.MachineryId, i.DisplayOrder }).IsUnique();

        builder.HasOne(i => i.Machinery)
            .WithMany(m => m.Images)
            .HasForeignKey(i => i.MachineryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MachineryRentalConfiguration : IEntityTypeConfiguration<MachineryRental>
{
    public void Configure(EntityTypeBuilder<MachineryRental> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MachineryRental_DateRange", "[EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_MachineryRental_RentalDays_Positive", "[RentalDays] > 0");
            table.HasCheckConstraint("CK_MachineryRental_Amounts_NonNegative", "[TotalRentAmount] >= 0 AND [TotalPayableAmount] >= 0 AND [SecurityDepositSnapshot] >= 0 AND [RentPerDaySnapshot] >= 0 AND [DriverChargePerDaySnapshot] >= 0 AND [MachineryAmount] >= 0 AND [DriverAmount] >= 0 AND [TotalAmount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(r => r.OwnerUserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.RenterUserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.RentPerDaySnapshot).HasPrecision(18, 2);
        builder.Property(r => r.DriverChargePerDaySnapshot).HasPrecision(18, 2);
        builder.Property(r => r.MachineryAmount).HasPrecision(18, 2);
        builder.Property(r => r.DriverAmount).HasPrecision(18, 2);
        builder.Property(r => r.TotalAmount).HasPrecision(18, 2);
        builder.Property(r => r.SecurityDepositSnapshot).HasPrecision(18, 2);
        builder.Property(r => r.TotalRentAmount).HasPrecision(18, 2);
        builder.Property(r => r.TotalPayableAmount).HasPrecision(18, 2);
        builder.Property(r => r.PaymentTransactionRef).HasMaxLength(200);
        builder.Property(r => r.CancellationReason).HasMaxLength(500);

        builder.HasIndex(r => r.MachineryId);
        builder.HasIndex(r => r.OwnerUserId);
        builder.HasIndex(r => r.RenterUserId);
        builder.HasIndex(r => r.RentalStatus);

        builder.HasOne(r => r.Machinery)
            .WithMany(m => m.Rentals)
            .HasForeignKey(r => r.MachineryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MachineryDamageReportConfiguration : IEntityTypeConfiguration<MachineryDamageReport>
{
    public void Configure(EntityTypeBuilder<MachineryDamageReport> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MachineryDamageReport_DamageAmount_NonNegative", "[DamageAmount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(r => r.ReportedByUserId).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.DamageAmount).HasPrecision(18, 2);

        builder.HasOne(r => r.MachineryRental)
            .WithMany(rental => rental.DamageReports)
            .HasForeignKey(r => r.MachineryRentalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MachineryDamageReportImageConfiguration : IEntityTypeConfiguration<MachineryDamageReportImage>
{
    public void Configure(EntityTypeBuilder<MachineryDamageReportImage> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(i => i.ImageUrl).HasMaxLength(500).IsRequired();
        builder.HasIndex(i => new { i.MachineryDamageReportId, i.DisplayOrder }).IsUnique();

        builder.HasOne(i => i.MachineryDamageReport)
            .WithMany(r => r.Images)
            .HasForeignKey(i => i.MachineryDamageReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
