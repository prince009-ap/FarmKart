using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;

namespace FarmKart.Domain.Entities;

public sealed class Auction : BaseEntity
{
    public Guid CropListingId { get; set; }
    public CropListing CropListing { get; set; } = null!;
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;
    public decimal StartingPrice { get; set; }
    public decimal CurrentHighestBid { get; set; }
    public decimal MinimumBidIncrement { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public AuctionStatus AuctionStatus { get; set; } = AuctionStatus.Draft;

    public ICollection<Bid> Bids { get; set; } = [];
    public AuctionWinner? AuctionWinner { get; set; }
}

public sealed class Bid : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime BidTimeUtc { get; set; } = DateTime.UtcNow;
    public BidStatus BidStatus { get; set; } = BidStatus.Active;
}

public sealed class AuctionWinner : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public Guid WinningBidId { get; set; }
    public Bid WinningBid { get; set; } = null!;
    public decimal FinalAmount { get; set; }
    public DateTime SelectedAtUtc { get; set; } = DateTime.UtcNow;
}
