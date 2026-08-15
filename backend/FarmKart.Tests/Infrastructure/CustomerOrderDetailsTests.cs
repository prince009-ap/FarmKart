using System;
using System.Collections.Generic;
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

public sealed class CustomerOrderDetailsTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private OrderService _orderService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_CustOrderDetailsTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _dbContext = new FarmKartDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
        _orderService = new OrderService(_dbContext, new NotificationService(_dbContext));
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetCustomerOrderDetailsAsync_ValidCustomer_ReturnsCorrectOrderDetailsAndTimelineData()
    {
        // Arrange
        var customerUser = new ApplicationUser { UserName = "cust1@farmkart.com", Email = "cust1@farmkart.com" };
        var farmerUser = new ApplicationUser { UserName = "farmer1@farmkart.com", Email = "farmer1@farmkart.com" };
        _dbContext.Users.AddRange(customerUser, farmerUser);
        await _dbContext.SaveChangesAsync();

        var customerProfile = new CustomerProfile { UserId = customerUser.Id, FullName = "Archi Vasoya" };
        var farmerProfile = new FarmerProfile { UserId = farmerUser.Id, FullName = "Prince Patel", FarmLocation = "Gujarat" };
        _dbContext.CustomerProfiles.Add(customerProfile);
        _dbContext.FarmerProfiles.Add(farmerProfile);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop { FarmerProfileId = farmerProfile.Id, CropName = "Wheat", CropType = "Grain", Variety = "Sharbati" };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmerProfile.Id, CropId = crop.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmerProfile.Id,
            StartingPrice = 500,
            MinimumBidIncrement = 20,
            StartTimeUtc = DateTime.UtcNow.AddDays(-2),
            EndTimeUtc = DateTime.UtcNow.AddDays(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile.Id,
            Amount = 600,
            RequestedQuantityKg = 300,
            BidStatus = BidStatus.Winning
        };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            BidId = bid.Id,
            CustomerProfileId = customerProfile.Id,
            RequestedQuantityKg = 300,
            AllocatedQuantityKg = 250,
            WinningBidAmountPerMan = 600,
            Status = AllocationStatus.PartiallyWon
        };
        _dbContext.AuctionAllocations.Add(allocation);
        await _dbContext.SaveChangesAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile.Id,
            Amount = 7500,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "FK-TEST-123456",
            PaymentMethod = PaymentMethod.Card,
            PaidAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var order = new AuctionOrder
        {
            OrderNumber = "FK-20260815-0001",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customerProfile.Id,
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            AllocatedQuantityKg = 250,
            PricePerMan = 600,
            TotalAmount = 7500,
            Status = OrderStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orderService.GetCustomerOrderDetailsAsync(customerUser.Id, order.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal("FK-20260815-0001", result.OrderNumber);
        Assert.Equal("Wheat", result.CropName);
        Assert.Equal("Sharbati", result.Variety);
        Assert.Equal(300, result.RequestedQuantityKg);
        Assert.Equal(15, result.RequestedQuantityMan);
        Assert.Equal(250, result.AllocatedQuantityKg);
        Assert.Equal(12.5m, result.AllocatedQuantityMan);
        Assert.Equal(600, result.PricePerMan);
        Assert.Equal(7500, result.TotalAmount);
        Assert.Equal("CONFIRMED", result.Status);
        Assert.Equal("PAID", result.PaymentStatus);
        Assert.Equal("FK-TEST-123456", result.TransactionReference);
        Assert.Equal("Prince Patel", result.FarmerName);
    }

    [Fact]
    public async Task GetCustomerOrderDetailsAsync_UnauthorizedCustomer_ThrowsKeyNotFoundException()
    {
        // Arrange
        var customerUser1 = new ApplicationUser { UserName = "cust1@farmkart.com", Email = "cust1@farmkart.com" };
        var customerUser2 = new ApplicationUser { UserName = "cust2@farmkart.com", Email = "cust2@farmkart.com" };
        var farmerUser = new ApplicationUser { UserName = "farmer1@farmkart.com", Email = "farmer1@farmkart.com" };
        _dbContext.Users.AddRange(customerUser1, customerUser2, farmerUser);
        await _dbContext.SaveChangesAsync();

        var customerProfile1 = new CustomerProfile { UserId = customerUser1.Id, FullName = "Archi Vasoya" };
        var customerProfile2 = new CustomerProfile { UserId = customerUser2.Id, FullName = "Other Customer" };
        var farmerProfile = new FarmerProfile { UserId = farmerUser.Id, FullName = "Prince Patel" };
        _dbContext.CustomerProfiles.AddRange(customerProfile1, customerProfile2);
        _dbContext.FarmerProfiles.Add(farmerProfile);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop { FarmerProfileId = farmerProfile.Id, CropName = "Wheat", CropType = "Grain" };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmerProfile.Id, CropId = crop.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmerProfile.Id,
            StartingPrice = 500,
            MinimumBidIncrement = 20,
            StartTimeUtc = DateTime.UtcNow.AddDays(-2),
            EndTimeUtc = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid { AuctionId = auction.Id, CustomerProfileId = customerProfile1.Id, Amount = 600, RequestedQuantityKg = 250, BidStatus = BidStatus.Winning };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var allocation = new AuctionAllocation { AuctionId = auction.Id, BidId = bid.Id, CustomerProfileId = customerProfile1.Id, RequestedQuantityKg = 250, AllocatedQuantityKg = 250, WinningBidAmountPerMan = 600, Status = AllocationStatus.Won };
        _dbContext.AuctionAllocations.Add(allocation);
        await _dbContext.SaveChangesAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customerProfile1.Id,
            Amount = 7500,
            PaymentStatus = PaymentStatus.Paid
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var order = new AuctionOrder
        {
            OrderNumber = "FK-20260815-0001",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customerProfile1.Id,
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            AllocatedQuantityKg = 250,
            PricePerMan = 600,
            TotalAmount = 7500,
            Status = OrderStatus.Confirmed
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act & Assert (Customer 2 attempting to view Customer 1's order)
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.GetCustomerOrderDetailsAsync(customerUser2.Id, order.Id));
    }
}
