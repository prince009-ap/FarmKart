using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class FarmerProfileConfiguration : IEntityTypeConfiguration<FarmerProfile>
{
    public void Configure(EntityTypeBuilder<FarmerProfile> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_FarmerProfile_FarmSize_NonNegative", "[FarmSize] IS NULL OR [FarmSize] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(profile => profile.UserId).IsRequired();
        builder.Property(profile => profile.FullName).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.Phone).HasMaxLength(20).IsRequired();
        builder.Property(profile => profile.ProfileImageUrl).HasMaxLength(500);
        builder.Property(profile => profile.FarmName).HasMaxLength(150);
        builder.Property(profile => profile.FarmSize).HasPrecision(18, 2);
        builder.Property(profile => profile.FarmSizeUnit)
            .HasConversion<int>()
            .HasColumnName("FarmSizeUnit");
        builder.Property(profile => profile.FarmLocation).HasMaxLength(250);

        builder.HasIndex(profile => profile.UserId).IsUnique();
        builder.ConfigureAddressInfo(profile => profile.AddressInfo);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<FarmerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkerProfileConfiguration : IEntityTypeConfiguration<WorkerProfile>
{
    public void Configure(EntityTypeBuilder<WorkerProfile> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_WorkerProfile_ExperienceYears_NonNegative", "[ExperienceYears] >= 0");
            table.HasCheckConstraint("CK_WorkerProfile_ExpectedDailyWage_NonNegative", "[ExpectedDailyWage] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(profile => profile.UserId).IsRequired();
        builder.Property(profile => profile.FullName).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.Phone).HasMaxLength(20).IsRequired();
        builder.Property(profile => profile.ProfileImageUrl).HasMaxLength(500);
        builder.Property(profile => profile.ExpectedDailyWage).HasPrecision(18, 2);
        builder.Property(profile => profile.AvailabilityNotes).HasMaxLength(500);
        builder.Property(profile => profile.ExperienceDescription).HasMaxLength(2000);

        builder.HasIndex(profile => profile.UserId).IsUnique();
        builder.ConfigureAddressInfo(profile => profile.AddressInfo);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<WorkerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(profile => profile.UserId).IsRequired();
        builder.Property(profile => profile.FullName).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.Phone).HasMaxLength(20).IsRequired();
        builder.Property(profile => profile.ProfileImageUrl).HasMaxLength(500);

        builder.HasIndex(profile => profile.UserId).IsUnique();
        builder.ConfigureAddressInfo(profile => profile.AddressInfo);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CustomerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
