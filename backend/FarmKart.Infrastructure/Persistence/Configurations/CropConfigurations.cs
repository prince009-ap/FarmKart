using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class CropConfiguration : IEntityTypeConfiguration<Crop>
{
    public void Configure(EntityTypeBuilder<Crop> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Crop_Area_NonNegative", "[Area] >= 0");
            table.HasCheckConstraint("CK_Crop_Quantity_NonNegative", "[Quantity] >= 0");
            table.HasCheckConstraint("CK_Crop_ExpectedHarvest_After_Sowing", "[ExpectedHarvestDate] IS NULL OR [SowingDate] IS NULL OR [ExpectedHarvestDate] >= [SowingDate]");
            table.HasCheckConstraint("CK_Crop_ActualHarvest_After_Sowing", "[ActualHarvestDate] IS NULL OR [SowingDate] IS NULL OR [ActualHarvestDate] >= [SowingDate]");
        });

        builder.ConfigureBaseEntity();

        builder.Property(crop => crop.CropName).HasMaxLength(120).IsRequired();
        builder.Property(crop => crop.CropType).HasMaxLength(120).HasDefaultValue("Other").IsRequired();
        builder.Property(crop => crop.Variety).HasMaxLength(120);
        builder.Property(crop => crop.Area).HasPrecision(18, 2);
        builder.Property(crop => crop.AreaUnit).HasDefaultValue(FarmKart.Domain.Enums.FarmSizeUnit.Acre);
        builder.Property(crop => crop.Quantity).HasPrecision(18, 2);
        builder.Property(crop => crop.QualityGrade).HasMaxLength(50);
        builder.Property(crop => crop.Description).HasMaxLength(1000);

        builder.HasIndex(crop => crop.FarmerProfileId);

        builder.HasOne(crop => crop.FarmerProfile)
            .WithMany(farmer => farmer.Crops)
            .HasForeignKey(crop => crop.FarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CropListingConfiguration : IEntityTypeConfiguration<CropListing>
{
    public void Configure(EntityTypeBuilder<CropListing> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_CropListing_QuantityForSale_NonNegative", "[QuantityForSale] >= 0");
            table.HasCheckConstraint("CK_CropListing_PricePerUnit_NonNegative", "[PricePerUnit] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(listing => listing.QuantityForSale).HasPrecision(18, 2);
        builder.Property(listing => listing.PricePerUnit).HasPrecision(18, 2);
        builder.Property(listing => listing.Description).HasMaxLength(1000);

        builder.HasIndex(listing => listing.FarmerProfileId);
        builder.HasIndex(listing => listing.CropId);
        builder.HasIndex(listing => listing.ListingType);

        builder.HasOne(listing => listing.FarmerProfile)
            .WithMany(farmer => farmer.CropListings)
            .HasForeignKey(listing => listing.FarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(listing => listing.Crop)
            .WithMany(crop => crop.Listings)
            .HasForeignKey(listing => listing.CropId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CropImageConfiguration : IEntityTypeConfiguration<CropImage>
{
    public void Configure(EntityTypeBuilder<CropImage> builder)
    {
        builder.ConfigureBaseEntity();

        builder.Property(image => image.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(image => image.IsPrimary).HasDefaultValue(false);
        builder.HasIndex(image => new { image.CropId, image.DisplayOrder }).IsUnique();

        builder.HasOne(image => image.Crop)
            .WithMany(crop => crop.Images)
            .HasForeignKey(image => image.CropId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CropStockTransactionConfiguration : IEntityTypeConfiguration<CropStockTransaction>
{
    public void Configure(EntityTypeBuilder<CropStockTransaction> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_CropStockTransaction_Quantity_NonZero", "[Quantity] <> 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(t => t.Quantity).HasPrecision(18, 2);
        builder.Property(t => t.QuantityInBaseUnit).HasPrecision(18, 2);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasIndex(t => t.CropId);

        builder.HasOne(t => t.Crop)
            .WithMany(crop => crop.StockTransactions)
            .HasForeignKey(t => t.CropId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

