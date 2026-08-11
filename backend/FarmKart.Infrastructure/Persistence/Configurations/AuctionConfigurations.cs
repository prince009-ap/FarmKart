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
