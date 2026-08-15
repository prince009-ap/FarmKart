using FarmKart.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FarmKart.Infrastructure.Persistence.Configurations;

public sealed class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Auction_Prices_Valid", "[StartingPrice] >= 0 AND [CurrentHighestBid] >= 0 AND [MinimumBidIncrement] > 0");
            table.HasCheckConstraint("CK_Auction_EndTime_After_StartTime", "[EndTimeUtc] > [StartTimeUtc]");
        });

        builder.ConfigureBaseEntity();

        builder.Property(auction => auction.StartingPrice).HasPrecision(18, 2);
        builder.Property(auction => auction.CurrentHighestBid).HasPrecision(18, 2);
        builder.Property(auction => auction.MinimumBidIncrement).HasPrecision(18, 2);

        builder.HasIndex(auction => auction.CropListingId).IsUnique();

        builder.HasOne(auction => auction.CropListing)
            .WithOne(listing => listing.Auction)
            .HasForeignKey<Auction>(auction => auction.CropListingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(auction => auction.FarmerProfile)
            .WithMany(farmer => farmer.Auctions)
            .HasForeignKey(auction => auction.FarmerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Bid_Amount_Positive", "[Amount] > 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(bid => bid.Amount).HasPrecision(18, 2);
        builder.Property(bid => bid.RequestedQuantityKg).HasPrecision(18, 2);
        builder.HasIndex(bid => bid.AuctionId);
        builder.HasIndex(bid => bid.CustomerProfileId);

        builder.HasOne(bid => bid.Auction)
            .WithMany(auction => auction.Bids)
            .HasForeignKey(bid => bid.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bid => bid.CustomerProfile)
            .WithMany(customer => customer.Bids)
            .HasForeignKey(bid => bid.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuctionAllocationConfiguration : IEntityTypeConfiguration<AuctionAllocation>
{
    public void Configure(EntityTypeBuilder<AuctionAllocation> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AuctionAllocation_Quantities_Valid", "[RequestedQuantityKg] > 0 AND [AllocatedQuantityKg] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(a => a.RequestedQuantityKg).HasPrecision(18, 2);
        builder.Property(a => a.AllocatedQuantityKg).HasPrecision(18, 2);
        builder.Property(a => a.WinningBidAmountPerMan).HasPrecision(18, 2);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(a => a.AuctionId);
        builder.HasIndex(a => a.CustomerProfileId);
        builder.HasIndex(a => a.BidId);

        builder.HasOne(a => a.Auction)
            .WithMany(auction => auction.Allocations)
            .HasForeignKey(a => a.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CustomerProfile)
            .WithMany()
            .HasForeignKey(a => a.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Bid)
            .WithMany()
            .HasForeignKey(a => a.BidId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuctionWinnerConfiguration : IEntityTypeConfiguration<AuctionWinner>
{
    public void Configure(EntityTypeBuilder<AuctionWinner> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AuctionWinner_FinalAmount_NonNegative", "[FinalAmount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(winner => winner.FinalAmount).HasPrecision(18, 2);
        builder.HasIndex(winner => winner.AuctionId).IsUnique();
        builder.HasIndex(winner => winner.WinningBidId).IsUnique();

        builder.HasOne(winner => winner.Auction)
            .WithOne(auction => auction.AuctionWinner)
            .HasForeignKey<AuctionWinner>(winner => winner.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(winner => winner.CustomerProfile)
            .WithMany(customer => customer.AuctionWins)
            .HasForeignKey(winner => winner.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(winner => winner.WinningBid)
            .WithMany()
            .HasForeignKey(winner => winner.WinningBidId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuctionPaymentConfiguration : IEntityTypeConfiguration<AuctionPayment>
{
    public void Configure(EntityTypeBuilder<AuctionPayment> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AuctionPayment_Amount_NonNegative", "[Amount] >= 0");
        });

        builder.ConfigureBaseEntity();

        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.AllocatedQuantityKg).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(10);
        builder.Property(payment => payment.TransactionReference).HasMaxLength(150);

        builder.HasIndex(payment => new { payment.AuctionId, payment.CustomerProfileId });

        builder.HasOne(payment => payment.Auction)
            .WithMany(auction => auction.AuctionPayments)
            .HasForeignKey(payment => payment.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(payment => payment.CustomerProfile)
            .WithMany()
            .HasForeignKey(payment => payment.CustomerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
