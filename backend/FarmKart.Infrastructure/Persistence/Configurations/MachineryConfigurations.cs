using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class MachineryCategoryConfiguration : IEntityTypeConfiguration<MachineryCategory>
{
    public void Configure(EntityTypeBuilder<MachineryCategory> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(500);

        builder.HasIndex(category => category.Name).IsUnique();
    }
}

public sealed class MachineryConfiguration : IEntityTypeConfiguration<Machinery>
{
    public void Configure(EntityTypeBuilder<Machinery> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Machinery_RentValues_NonNegative", "[DailyRent] >= 0 AND ([WeeklyRent] IS NULL OR [WeeklyRent] >= 0) AND ([MonthlyRent] IS NULL OR [MonthlyRent] >= 0) AND [SecurityDeposit] >= 0");
            table.HasCheckConstraint("CK_Machinery_ManufacturingYear_Range", "[ManufacturingYear] IS NULL OR ([ManufacturingYear] >= 1900 AND [ManufacturingYear] <= 2100)");
        });

        builder.ConfigureBaseEntity();

        builder.Property(machine => machine.Name).HasMaxLength(150).IsRequired();
        builder.Property(machine => machine.Brand).HasMaxLength(100);
        builder.Property(machine => machine.Model).HasMaxLength(100);
        builder.Property(machine => machine.Description).HasMaxLength(2000);
        builder.Property(machine => machine.DailyRent).HasPrecision(18, 2);
        builder.Property(machine => machine.WeeklyRent).HasPrecision(18, 2);
        builder.Property(machine => machine.MonthlyRent).HasPrecision(18, 2);
        builder.Property(machine => machine.SecurityDeposit).HasPrecision(18, 2);
        builder.Property(machine => machine.Location).HasMaxLength(250).IsRequired();

        builder.HasIndex(machine => machine.OwnerFarmerProfileId);
        builder.HasIndex(machine => machine.MachineryCategoryId);

        builder.HasOne(machine => machine.OwnerFarmerProfile)
            .WithMany(farmer => farmer.OwnedMachinery)
            .HasForeignKey(machine => machine.OwnerFarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(machine => machine.MachineryCategory)
            .WithMany(category => category.MachineryItems)
            .HasForeignKey(machine => machine.MachineryCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MachineryImageConfiguration : IEntityTypeConfiguration<MachineryImage>
{
    public void Configure(EntityTypeBuilder<MachineryImage> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(image => image.ImageUrl).HasMaxLength(500).IsRequired();
        builder.HasIndex(image => new { image.MachineryId, image.DisplayOrder }).IsUnique();

        builder.HasOne(image => image.Machinery)
            .WithMany(machine => machine.Images)
            .HasForeignKey(image => image.MachineryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MachineryRentalRequestConfiguration : IEntityTypeConfiguration<MachineryRentalRequest>
{
    public void Configure(EntityTypeBuilder<MachineryRentalRequest> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MachineryRentalRequest_DateRange", "[EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_MachineryRentalRequest_Amounts_NonNegative", "[RequestedAmount] >= 0 AND [SecurityDeposit] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(request => request.RequestedAmount).HasPrecision(18, 2);
        builder.Property(request => request.SecurityDeposit).HasPrecision(18, 2);
        builder.Property(request => request.Message).HasMaxLength(1000);

        builder.HasIndex(request => request.MachineryId);
        builder.HasIndex(request => request.RenterFarmerProfileId);
        builder.HasIndex(request => request.OwnerFarmerProfileId);

        builder.HasOne(request => request.Machinery)
            .WithMany(machine => machine.RentalRequests)
            .HasForeignKey(request => request.MachineryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.RenterFarmerProfile)
            .WithMany(farmer => farmer.MachineryRentalRequestsAsRenter)
            .HasForeignKey(request => request.RenterFarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(request => request.OwnerFarmerProfile)
            .WithMany(farmer => farmer.MachineryRentalRequestsAsOwner)
            .HasForeignKey(request => request.OwnerFarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MachineryRentalConfiguration : IEntityTypeConfiguration<MachineryRental>
{
    public void Configure(EntityTypeBuilder<MachineryRental> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MachineryRental_DateRange", "[EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_MachineryRental_Amounts_NonNegative", "[TotalAmount] >= 0 AND [SecurityDeposit] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(rental => rental.TotalAmount).HasPrecision(18, 2);
        builder.Property(rental => rental.SecurityDeposit).HasPrecision(18, 2);

        builder.HasIndex(rental => rental.MachineryRentalRequestId)
            .IsUnique()
            .HasFilter("[MachineryRentalRequestId] IS NOT NULL");

        builder.HasOne(rental => rental.Machinery)
            .WithMany(machine => machine.Rentals)
            .HasForeignKey(rental => rental.MachineryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rental => rental.OwnerFarmerProfile)
            .WithMany(farmer => farmer.MachineryRentalsAsOwner)
            .HasForeignKey(rental => rental.OwnerFarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rental => rental.RenterFarmerProfile)
            .WithMany(farmer => farmer.MachineryRentalsAsRenter)
            .HasForeignKey(rental => rental.RenterFarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(rental => rental.MachineryRentalRequest)
            .WithOne(request => request.MachineryRental)
            .HasForeignKey<MachineryRental>(rental => rental.MachineryRentalRequestId)
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

        builder.Property(report => report.ReportedByUserId).HasMaxLength(128).IsRequired();
        builder.Property(report => report.Description).HasMaxLength(2000).IsRequired();
        builder.Property(report => report.DamageAmount).HasPrecision(18, 2);

        builder.HasOne(report => report.MachineryRental)
            .WithMany(rental => rental.DamageReports)
            .HasForeignKey(report => report.MachineryRentalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MachineryDamageReportImageConfiguration : IEntityTypeConfiguration<MachineryDamageReportImage>
{
    public void Configure(EntityTypeBuilder<MachineryDamageReportImage> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(image => image.ImageUrl).HasMaxLength(500).IsRequired();
        builder.HasIndex(image => new { image.MachineryDamageReportId, image.DisplayOrder }).IsUnique();

        builder.HasOne(image => image.MachineryDamageReport)
            .WithMany(report => report.Images)
            .HasForeignKey(image => image.MachineryDamageReportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
