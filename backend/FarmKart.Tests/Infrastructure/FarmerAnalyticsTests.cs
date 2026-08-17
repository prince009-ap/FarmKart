using System;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class FarmerAnalyticsTests : IDisposable
{
    private readonly FarmKartDbContext _db;
    private readonly FarmerAnalyticsService _service;
    private readonly string _dbName;

    public FarmerAnalyticsTests()
    {
        _dbName = $"FarmKartDb_FarmerAnalyticsTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _db = new FarmKartDbContext(options);
        _db.Database.EnsureCreated();
        _service = new FarmerAnalyticsService(_db);
    }

    [Fact]
    public async Task GetFarmerAnalyticsAsync_ValidFarmer_ReturnsRealCalculatedMetrics()
    {
        // Arrange
        var farmerUserId = Guid.NewGuid();
        var farmerUser = new ApplicationUser
        {
            Id = farmerUserId,
            UserName = "farmer_analytics_user@test.com",
            Email = "farmer_analytics_user@test.com"
        };
        _db.Users.Add(farmerUser);

        var farmerProfile = new FarmerProfile
        {
            UserId = farmerUserId,
            FullName = "Test Farmer Analytics",
            FarmName = "Green Fields"
        };
        _db.FarmerProfiles.Add(farmerProfile);

        var crop = new Crop
        {
            FarmerProfileId = farmerProfile.Id,
            CropName = "Organic Wheat",
            CropType = "Grains",
            Variety = "Sharbati"
        };
        _db.Crops.Add(crop);

        var cropListing = new CropListing
        {
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            QuantityForSale = 1000m,
            Unit = MeasurementUnit.Kilogram,
            PricePerUnit = 40m,
            Description = "Premium Wheat"
        };
        _db.CropListings.Add(cropListing);

        var auction = new Auction
        {
            FarmerProfileId = farmerProfile.Id,
            CropListingId = cropListing.Id,
            StartingPrice = 30m,
            CurrentHighestBid = 45m,
            MinimumBidIncrement = 1m,
            StartTimeUtc = DateTime.UtcNow.AddDays(-5),
            EndTimeUtc = DateTime.UtcNow.AddDays(-1),
            AuctionStatus = AuctionStatus.Finalized
        };
        _db.Auctions.Add(auction);

        var customerUserId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser { Id = customerUserId, UserName = "buyer_customer@test.com", Email = "buyer_customer@test.com" });
        var customerProfile = new CustomerProfile
        {
            UserId = customerUserId,
            FullName = "Buyer Customer"
        };
        _db.CustomerProfiles.Add(customerProfile);

        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile.Id,
            Amount = 800m,
            RequestedQuantityKg = 500m,
            BidTimeUtc = DateTime.UtcNow.AddDays(-3),
            BidStatus = BidStatus.Active
        };
        _db.Bids.Add(bid);

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            BidId = bid.Id,
            CustomerProfileId = customerProfile.Id,
            RequestedQuantityKg = 500m,
            AllocatedQuantityKg = 500m,
            WinningBidAmountPerMan = 800m,
            FinalizedAtUtc = DateTime.UtcNow.AddDays(-2),
            Status = AllocationStatus.Won
        };
        _db.AuctionAllocations.Add(allocation);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile.Id,
            Amount = 20000m,
            AllocatedQuantityKg = 500m,
            PaymentStatus = PaymentStatus.Paid,
            PaidAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        _db.AuctionPayments.Add(payment);

        var order = new AuctionOrder
        {
            OrderNumber = "ORD-FAR-1001",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customerProfile.Id,
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            AllocatedQuantityKg = 500m,
            PricePerMan = 800m,
            TotalAmount = 20000m,
            Status = OrderStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        _db.AuctionOrders.Add(order);

        // Machinery owned by Farmer
        var machinery = new Machinery
        {
            OwnerUserId = farmerUserId.ToString(),
            Name = "John Deere Tractor",
            Category = "Tractor",
            DailyRent = 2000m,
            DriverAvailable = true,
            DriverChargePerDay = 500m,
            AvailabilityStatus = MachineryAvailabilityStatus.Available,
            IsActive = true
        };
        _db.Machinery.Add(machinery);

        var rental = new MachineryRental
        {
            MachineryId = machinery.Id,
            OwnerUserId = farmerUserId.ToString(),
            RenterUserId = customerUserId.ToString(),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            RentalDays = 2,
            DriverRequired = true,
            MachineryAmount = 4000m,
            DriverAmount = 1000m,
            TotalPayableAmount = 5000m,
            RentalStatus = RentalStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-4)
        };
        _db.MachineryRentals.Add(rental);

        // Order Review for Farmer
        var review = new Review
        {
            ReviewerUserId = customerUserId.ToString(),
            RevieweeUserId = farmerUserId.ToString(),
            Rating = 5,
            Comment = "Excellent produce and smooth delivery!",
            RelatedEntityType = ReviewEntityType.Order,
            RelatedEntityId = order.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        _db.Reviews.Add(review);

        await _db.SaveChangesAsync();

        // Act
        var request = new AnalyticsDateRangeRequest(AnalyticsDateRange.Last30Days);
        var analytics = await _service.GetFarmerAnalyticsAsync(farmerUserId.ToString(), request);

        // Assert
        Assert.NotNull(analytics);
        Assert.Equal(1, analytics.TotalAuctions);
        Assert.Equal(1, analytics.CompletedAuctions);
        Assert.Equal(1000m, analytics.TotalQuantityListedKg);
        Assert.Equal(500m, analytics.TotalQuantitySoldKg);
        Assert.Equal(25m, analytics.TotalQuantitySoldMan); // 500 Kg / 20 = 25 Man
        Assert.Equal(500m, analytics.TotalQuantityRemainingKg);

        Assert.Equal(1, analytics.TotalOrders);
        Assert.Equal(1, analytics.CompletedOrders);
        Assert.Equal(20000m, analytics.TotalRevenue);

        Assert.Equal(5.0, analytics.AverageFarmerRating);
        Assert.Equal(1, analytics.TotalFarmerReviews);
        Assert.Equal(1, analytics.FarmerRatingDistribution.FiveStar);

        Assert.Equal(1, analytics.MachineryListedCount);
        Assert.Equal(1, analytics.CompletedMachineryRentalsCount);
        Assert.Equal(5000m, analytics.MachineryRentalIncome);
        Assert.Equal(1, analytics.RentalsWithDriverCount);
        Assert.Equal(1000m, analytics.DriverRevenue);
    }

    [Fact]
    public async Task GetFarmerAnalyticsAsync_EmptyFarmer_ReturnsZeroedMetricsNoCrash()
    {
        // Arrange
        var emptyFarmerUserId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser { Id = emptyFarmerUserId, UserName = "empty_farmer@test.com", Email = "empty_farmer@test.com" });
        var farmerProfile = new FarmerProfile
        {
            UserId = emptyFarmerUserId,
            FullName = "Empty Farmer"
        };
        _db.FarmerProfiles.Add(farmerProfile);
        await _db.SaveChangesAsync();

        // Act
        var request = new AnalyticsDateRangeRequest(AnalyticsDateRange.Last7Days);
        var analytics = await _service.GetFarmerAnalyticsAsync(emptyFarmerUserId.ToString(), request);

        // Assert
        Assert.NotNull(analytics);
        Assert.Equal(0, analytics.TotalAuctions);
        Assert.Equal(0m, analytics.TotalQuantityListedKg);
        Assert.Equal(0m, analytics.TotalQuantitySoldKg);
        Assert.Equal(0m, analytics.TotalRevenue);
        Assert.Equal(0.0, analytics.AverageFarmerRating);
        Assert.Equal(0, analytics.MachineryListedCount);
        Assert.Equal(0m, analytics.MachineryRentalIncome);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
