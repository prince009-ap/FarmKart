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
    public ICollection<AuctionAllocation> Allocations { get; set; } = [];
    public AuctionWinner? AuctionWinner { get; set; }
    public ICollection<AuctionPayment> AuctionPayments { get; set; } = [];
    public AuctionPayment? AuctionPayment => AuctionPayments.FirstOrDefault();
}

public sealed class Bid : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal RequestedQuantityKg { get; set; }
    public DateTime BidTimeUtc { get; set; } = DateTime.UtcNow;
    public BidStatus BidStatus { get; set; } = BidStatus.Active;
}

public sealed class AuctionAllocation : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid BidId { get; set; }
    public Bid Bid { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public decimal RequestedQuantityKg { get; set; }
    public decimal AllocatedQuantityKg { get; set; }
    public decimal WinningBidAmountPerMan { get; set; }
    public AllocationStatus Status { get; set; } = AllocationStatus.Won;
    public DateTime FinalizedAtUtc { get; set; } = DateTime.UtcNow;
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

public sealed class AuctionPayment : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal AllocatedQuantityKg { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Other;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string TransactionReference { get; set; } = string.Empty;
    public DateTime? PaidAtUtc { get; set; }

    public AuctionOrder? AuctionOrder { get; set; }
}

public sealed class AuctionOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid AuctionAllocationId { get; set; }
    public AuctionAllocation AuctionAllocation { get; set; } = null!;
    public Guid AuctionPaymentId { get; set; }
    public AuctionPayment AuctionPayment { get; set; } = null!;
    public Guid CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public Guid FarmerProfileId { get; set; }
    public FarmerProfile FarmerProfile { get; set; } = null!;
    public Guid CropId { get; set; }
    public Crop Crop { get; set; } = null!;
    public decimal AllocatedQuantityKg { get; set; }
    public decimal PricePerMan { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
}

