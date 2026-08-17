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

public class CustomerAnalyticsTests : IDisposable
{
    private readonly FarmKartDbContext _db;
    private readonly CustomerAnalyticsService _service;
    private readonly string _dbName;

    public CustomerAnalyticsTests()
    {
        _dbName = $"FarmKartDb_CustomerAnalyticsTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _db = new FarmKartDbContext(options);
        _db.Database.EnsureCreated();
        _service = new CustomerAnalyticsService(_db);
    }

    [Fact]
    public async Task GetCustomerAnalyticsAsync_ValidCustomer_ReturnsRealCalculatedMetrics()
    {
        // Arrange
        var customerUserId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser { Id = customerUserId, UserName = "analytics_cust@test.com", Email = "analytics_cust@test.com" });
        var customerProfile = new CustomerProfile
        {
            UserId = customerUserId,
            FullName = "Analytics Customer"
        };
        _db.CustomerProfiles.Add(customerProfile);

        var farmerUserId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser { Id = farmerUserId, UserName = "supplier_farmer@test.com", Email = "supplier_farmer@test.com" });
        var farmerProfile = new FarmerProfile
        {
            UserId = farmerUserId,
            FullName = "Supplier Farmer"
        };
        _db.FarmerProfiles.Add(farmerProfile);

        var crop = new Crop
        {
            FarmerProfileId = farmerProfile.Id,
            CropName = "Basmati Rice",
            CropType = "Grains",
            Variety = "1121"
        };
        _db.Crops.Add(crop);

        var cropListing = new CropListing
        {
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            QuantityForSale = 600m,
            Unit = MeasurementUnit.Kilogram,
            PricePerUnit = 60m
        };
        _db.CropListings.Add(cropListing);

        var auction = new Auction
        {
            FarmerProfileId = farmerProfile.Id,
            CropListingId = cropListing.Id,
            StartingPrice = 50m,
            CurrentHighestBid = 70m,
            MinimumBidIncrement = 1m,
            StartTimeUtc = DateTime.UtcNow.AddDays(-6),
            EndTimeUtc = DateTime.UtcNow.AddDays(-2),
            AuctionStatus = AuctionStatus.Finalized
        };
        _db.Auctions.Add(auction);

        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile.Id,
            Amount = 1400m, // Price per Man (20Kg)
            RequestedQuantityKg = 300m,
            BidTimeUtc = DateTime.UtcNow.AddDays(-5),
            BidStatus = BidStatus.Active
        };
        _db.Bids.Add(bid);

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            BidId = bid.Id,
            CustomerProfileId = customerProfile.Id,
            RequestedQuantityKg = 300m,
            AllocatedQuantityKg = 300m,
            WinningBidAmountPerMan = 1400m,
            FinalizedAtUtc = DateTime.UtcNow.AddDays(-2),
            Status = AllocationStatus.Won
        };
        _db.AuctionAllocations.Add(allocation);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile.Id,
            Amount = 21000m,
            AllocatedQuantityKg = 300m,
            PaymentStatus = PaymentStatus.Paid,
            PaidAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        _db.AuctionPayments.Add(payment);

        var order = new AuctionOrder
        {
            OrderNumber = "ORD-CUST-2002",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customerProfile.Id,
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            AllocatedQuantityKg = 300m,
            PricePerMan = 1400m,
            TotalAmount = 21000m,
            Status = OrderStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        _db.AuctionOrders.Add(order);

        // Customer Machinery Rental
        var machinery = new Machinery
        {
            OwnerUserId = farmerProfile.UserId.ToString(),
            Name = "Rotavator Heavy",
            Category = "Rotavator",
            DailyRent = 1500m,
            DriverAvailable = true,
            DriverChargePerDay = 400m,
            AvailabilityStatus = MachineryAvailabilityStatus.Available,
            IsActive = true
        };
        _db.Machinery.Add(machinery);

        var rental = new MachineryRental
        {
            MachineryId = machinery.Id,
            OwnerUserId = farmerProfile.UserId.ToString(),
            RenterUserId = customerUserId.ToString(),
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RentalDays = 2,
            DriverRequired = true,
            MachineryAmount = 3000m,
            DriverAmount = 800m,
            TotalPayableAmount = 3800m,
            RentalStatus = RentalStatus.Completed,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        _db.MachineryRentals.Add(rental);

        // Wishlist item
        var wishItem = new WishlistItem
        {
            UserId = customerUserId.ToString(),
            ItemType = WishlistItemType.Crop,
            ItemId = crop.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        _db.WishlistItems.Add(wishItem);

        await _db.SaveChangesAsync();

        // Act
        var request = new AnalyticsDateRangeRequest(AnalyticsDateRange.Last30Days);
        var analytics = await _service.GetCustomerAnalyticsAsync(customerUserId.ToString(), request);

        // Assert
        Assert.NotNull(analytics);
        Assert.Equal(1, analytics.TotalAuctionsParticipated);
        Assert.Equal(1, analytics.TotalBidsPlaced);
        Assert.Equal(1, analytics.WinningBidsCount);
        Assert.Equal(100.0, analytics.WinningRatePercentage); // 1 winning / 1 participated * 100

        Assert.Equal(300m, analytics.TotalQuantityPurchasedKg);
        Assert.Equal(15m, analytics.TotalQuantityPurchasedMan); // 300 / 20 = 15
        Assert.Equal(1, analytics.TotalCropOrders);
        Assert.Equal(1, analytics.CompletedOrders);
        Assert.Equal(21000m, analytics.TotalCropSpending);
        Assert.Equal(21000m, analytics.AverageOrderValue);
        Assert.Equal(21000m, analytics.HighestOrderValue);

        Assert.Equal(1, analytics.TotalMachineryRentals);
        Assert.Equal(1, analytics.CompletedRentalsCount);
        Assert.Equal(3800m, analytics.TotalMachineryRentalSpending);
        Assert.Equal(1, analytics.RentalsWithDriverCount);
        Assert.Equal(800m, analytics.DriverSpending);

        Assert.Equal(1, analytics.WishlistCount);
        Assert.Equal(1, analytics.CropWishlistCount);
    }

    [Fact]
    public async Task GetCustomerAnalyticsAsync_EmptyCustomer_ReturnsZeroedMetricsNoCrash()
    {
        // Arrange
        var emptyCustUserId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser { Id = emptyCustUserId, UserName = "empty_cust@test.com", Email = "empty_cust@test.com" });
        var customerProfile = new CustomerProfile
        {
            UserId = emptyCustUserId,
            FullName = "New Customer"
        };
        _db.CustomerProfiles.Add(customerProfile);
        await _db.SaveChangesAsync();

        // Act
        var request = new AnalyticsDateRangeRequest(AnalyticsDateRange.Last30Days);
        var analytics = await _service.GetCustomerAnalyticsAsync(emptyCustUserId.ToString(), request);

        // Assert
        Assert.NotNull(analytics);
        Assert.Equal(0, analytics.TotalAuctionsParticipated);
        Assert.Equal(0, analytics.TotalBidsPlaced);
        Assert.Equal(0.0, analytics.WinningRatePercentage);
        Assert.Equal(0m, analytics.TotalQuantityPurchasedKg);
        Assert.Equal(0m, analytics.TotalCropSpending);
        Assert.Equal(0, analytics.TotalMachineryRentals);
        Assert.Equal(0m, analytics.TotalMachineryRentalSpending);
        Assert.Equal(0, analytics.WishlistCount);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
