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

public sealed class FarmerOrdersTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private OrderService _orderService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_FarmerOrdersTest_{Guid.NewGuid():N}";
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
    public async Task GetFarmerOrdersAsync_ReturnsOnlyFarmerOwnOrders()
    {
        // Arrange
        var farmerUser1 = new ApplicationUser { UserName = "farmer1@farmkart.com", Email = "farmer1@farmkart.com" };
        var farmerUser2 = new ApplicationUser { UserName = "farmer2@farmkart.com", Email = "farmer2@farmkart.com" };
        var customerUser = new ApplicationUser { UserName = "cust@farmkart.com", Email = "cust@farmkart.com" };
        _dbContext.Users.AddRange(farmerUser1, farmerUser2, customerUser);
        await _dbContext.SaveChangesAsync();

        var farmerProfile1 = new FarmerProfile { UserId = farmerUser1.Id, FullName = "Farmer One" };
        var farmerProfile2 = new FarmerProfile { UserId = farmerUser2.Id, FullName = "Farmer Two" };
        var customerProfile = new CustomerProfile { UserId = customerUser.Id, FullName = "Customer A" };
        _dbContext.FarmerProfiles.AddRange(farmerProfile1, farmerProfile2);
        _dbContext.CustomerProfiles.Add(customerProfile);
        await _dbContext.SaveChangesAsync();

        var crop1 = new Crop { FarmerProfileId = farmerProfile1.Id, CropName = "Wheat", CropType = "Grain" };
        var crop2 = new Crop { FarmerProfileId = farmerProfile2.Id, CropName = "Rice", CropType = "Cereal" };
        _dbContext.Crops.AddRange(crop1, crop2);
        await _dbContext.SaveChangesAsync();

        var listing1 = new CropListing { FarmerProfileId = farmerProfile1.Id, CropId = crop1.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram };
        var listing2 = new CropListing { FarmerProfileId = farmerProfile2.Id, CropId = crop2.Id, QuantityForSale = 600, Unit = MeasurementUnit.Kilogram };
        _dbContext.CropListings.AddRange(listing1, listing2);
        await _dbContext.SaveChangesAsync();

        var auction1 = new Auction { CropListingId = listing1.Id, FarmerProfileId = farmerProfile1.Id, StartingPrice = 500, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow.AddDays(-2), EndTimeUtc = DateTime.UtcNow.AddDays(-1) };
        var auction2 = new Auction { CropListingId = listing2.Id, FarmerProfileId = farmerProfile2.Id, StartingPrice = 500, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow.AddDays(-2), EndTimeUtc = DateTime.UtcNow.AddDays(-1) };
        _dbContext.Auctions.AddRange(auction1, auction2);
        await _dbContext.SaveChangesAsync();

        var bid1 = new Bid { AuctionId = auction1.Id, CustomerProfileId = customerProfile.Id, Amount = 600, RequestedQuantityKg = 250, BidStatus = BidStatus.Winning };
        var bid2 = new Bid { AuctionId = auction2.Id, CustomerProfileId = customerProfile.Id, Amount = 690, RequestedQuantityKg = 350, BidStatus = BidStatus.Winning };
        _dbContext.Bids.AddRange(bid1, bid2);
        await _dbContext.SaveChangesAsync();

        var alloc1 = new AuctionAllocation { AuctionId = auction1.Id, BidId = bid1.Id, CustomerProfileId = customerProfile.Id, RequestedQuantityKg = 250, AllocatedQuantityKg = 250, WinningBidAmountPerMan = 600, Status = AllocationStatus.Won };
        var alloc2 = new AuctionAllocation { AuctionId = auction2.Id, BidId = bid2.Id, CustomerProfileId = customerProfile.Id, RequestedQuantityKg = 350, AllocatedQuantityKg = 350, WinningBidAmountPerMan = 690, Status = AllocationStatus.Won };
        _dbContext.AuctionAllocations.AddRange(alloc1, alloc2);
        await _dbContext.SaveChangesAsync();

        var payment1 = new AuctionPayment { AuctionId = auction1.Id, PaymentStatus = PaymentStatus.Paid, CustomerProfileId = customerProfile.Id };
        var payment2 = new AuctionPayment { AuctionId = auction2.Id, PaymentStatus = PaymentStatus.Paid, CustomerProfileId = customerProfile.Id };
        _dbContext.AuctionPayments.AddRange(payment1, payment2);
        await _dbContext.SaveChangesAsync();

        var order1 = new AuctionOrder
        {
            OrderNumber = "FK-FARMER1-001",
            AuctionId = auction1.Id,
            AuctionAllocationId = alloc1.Id,
            FarmerProfileId = farmerProfile1.Id,
            CustomerProfileId = customerProfile.Id,
            CropId = crop1.Id,
            AuctionPaymentId = payment1.Id,
            AllocatedQuantityKg = 250,
            PricePerMan = 600,
            TotalAmount = 7500,
            Status = OrderStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        var order2 = new AuctionOrder
        {
            OrderNumber = "FK-FARMER2-001",
            AuctionId = auction2.Id,
            AuctionAllocationId = alloc2.Id,
            FarmerProfileId = farmerProfile2.Id,
            CustomerProfileId = customerProfile.Id,
            CropId = crop2.Id,
            AuctionPaymentId = payment2.Id,
            AllocatedQuantityKg = 350,
            PricePerMan = 690,
            TotalAmount = 12075,
            Status = OrderStatus.Confirmed,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.AuctionOrders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result1 = await _orderService.GetFarmerOrdersAsync(farmerUser1.Id, new FarmerOrderFilterRequest());
        var result2 = await _orderService.GetFarmerOrdersAsync(farmerUser2.Id, new FarmerOrderFilterRequest());

        // Assert
        Assert.Single(result1);
        Assert.Equal("FK-FARMER1-001", result1[0].OrderNumber);
        Assert.Equal("Wheat", result1[0].CropName);

        Assert.Single(result2);
        Assert.Equal("FK-FARMER2-001", result2[0].OrderNumber);
        Assert.Equal("Rice", result2[0].CropName);
    }

    [Fact]
    public async Task GetFarmerOrdersAsync_MultipleWinnersFromSameAuction_ReturnsSeparateOrdersForFarmer()
    {
        // Arrange
        var farmerUser = new ApplicationUser { UserName = "farmer@farmkart.com", Email = "farmer@farmkart.com" };
        var custUser1 = new ApplicationUser { UserName = "cust1@farmkart.com", Email = "cust1@farmkart.com" };
        var custUser2 = new ApplicationUser { UserName = "cust2@farmkart.com", Email = "cust2@farmkart.com" };
        _dbContext.Users.AddRange(farmerUser, custUser1, custUser2);
        await _dbContext.SaveChangesAsync();

        var farmerProfile = new FarmerProfile { UserId = farmerUser.Id, FullName = "Farmer John" };
        var customer1 = new CustomerProfile { UserId = custUser1.Id, FullName = "Customer A" };
        var customer2 = new CustomerProfile { UserId = custUser2.Id, FullName = "Customer B" };
        _dbContext.FarmerProfiles.Add(farmerProfile);
        _dbContext.CustomerProfiles.AddRange(customer1, customer2);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop { FarmerProfileId = farmerProfile.Id, CropName = "Wheat", CropType = "Grain" };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmerProfile.Id, CropId = crop.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction { CropListingId = listing.Id, FarmerProfileId = farmerProfile.Id, StartingPrice = 500, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow.AddDays(-2), EndTimeUtc = DateTime.UtcNow.AddDays(-1) };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid1 = new Bid { AuctionId = auction.Id, CustomerProfileId = customer1.Id, Amount = 600, RequestedQuantityKg = 250, BidStatus = BidStatus.Winning };
        var bid2 = new Bid { AuctionId = auction.Id, CustomerProfileId = customer2.Id, Amount = 620, RequestedQuantityKg = 100, BidStatus = BidStatus.Winning };
        _dbContext.Bids.AddRange(bid1, bid2);
        await _dbContext.SaveChangesAsync();

        var alloc1 = new AuctionAllocation { AuctionId = auction.Id, BidId = bid1.Id, CustomerProfileId = customer1.Id, RequestedQuantityKg = 250, AllocatedQuantityKg = 250, WinningBidAmountPerMan = 600, Status = AllocationStatus.Won };
        var alloc2 = new AuctionAllocation { AuctionId = auction.Id, BidId = bid2.Id, CustomerProfileId = customer2.Id, RequestedQuantityKg = 100, AllocatedQuantityKg = 100, WinningBidAmountPerMan = 620, Status = AllocationStatus.Won };
        _dbContext.AuctionAllocations.AddRange(alloc1, alloc2);
        await _dbContext.SaveChangesAsync();

        var payment1 = new AuctionPayment { AuctionId = auction.Id, PaymentStatus = PaymentStatus.Paid, CustomerProfileId = customer1.Id };
        var payment2 = new AuctionPayment { AuctionId = auction.Id, PaymentStatus = PaymentStatus.Paid, CustomerProfileId = customer2.Id };
        _dbContext.AuctionPayments.AddRange(payment1, payment2);
        await _dbContext.SaveChangesAsync();

        var order1 = new AuctionOrder
        {
            OrderNumber = "FK-20260815-0001",
            AuctionId = auction.Id,
            AuctionAllocationId = alloc1.Id,
            FarmerProfileId = farmerProfile.Id,
            CustomerProfileId = customer1.Id,
            CropId = crop.Id,
            AuctionPaymentId = payment1.Id,
            AllocatedQuantityKg = 250,
            PricePerMan = 600,
            TotalAmount = 7500,
            Status = OrderStatus.Confirmed
        };

        var order2 = new AuctionOrder
        {
            OrderNumber = "FK-20260815-0002",
            AuctionId = auction.Id,
            AuctionAllocationId = alloc2.Id,
            FarmerProfileId = farmerProfile.Id,
            CustomerProfileId = customer2.Id,
            CropId = crop.Id,
            AuctionPaymentId = payment2.Id,
            AllocatedQuantityKg = 100,
            PricePerMan = 620,
            TotalAmount = 3100,
            Status = OrderStatus.Confirmed
        };

        _dbContext.AuctionOrders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _orderService.GetFarmerOrdersAsync(farmerUser.Id, new FarmerOrderFilterRequest());

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, o => o.OrderNumber == "FK-20260815-0001" && o.CustomerName == "Customer A");
        Assert.Contains(result, o => o.OrderNumber == "FK-20260815-0002" && o.CustomerName == "Customer B");
    }

    [Fact]
    public async Task GetFarmerOrderDetailsAsync_UnauthorizedFarmer_ThrowsKeyNotFoundException()
    {
        // Arrange
        var farmerUser1 = new ApplicationUser { UserName = "farmer1@farmkart.com", Email = "farmer1@farmkart.com" };
        var farmerUser2 = new ApplicationUser { UserName = "farmer2@farmkart.com", Email = "farmer2@farmkart.com" };
        var customerUser = new ApplicationUser { UserName = "cust@farmkart.com", Email = "cust@farmkart.com" };
        _dbContext.Users.AddRange(farmerUser1, farmerUser2, customerUser);
        await _dbContext.SaveChangesAsync();

        var farmerProfile1 = new FarmerProfile { UserId = farmerUser1.Id, FullName = "Farmer One" };
        var farmerProfile2 = new FarmerProfile { UserId = farmerUser2.Id, FullName = "Farmer Two" };
        var customerProfile = new CustomerProfile { UserId = customerUser.Id, FullName = "Customer A" };
        _dbContext.FarmerProfiles.AddRange(farmerProfile1, farmerProfile2);
        _dbContext.CustomerProfiles.Add(customerProfile);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop { FarmerProfileId = farmerProfile1.Id, CropName = "Wheat" };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmerProfile1.Id, CropId = crop.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction { CropListingId = listing.Id, FarmerProfileId = farmerProfile1.Id, StartingPrice = 500, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow.AddDays(-2), EndTimeUtc = DateTime.UtcNow.AddDays(-1) };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid { AuctionId = auction.Id, CustomerProfileId = customerProfile.Id, Amount = 600, RequestedQuantityKg = 250, BidStatus = BidStatus.Winning };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var alloc = new AuctionAllocation { AuctionId = auction.Id, BidId = bid.Id, CustomerProfileId = customerProfile.Id, RequestedQuantityKg = 250, AllocatedQuantityKg = 250, WinningBidAmountPerMan = 600, Status = AllocationStatus.Won };
        _dbContext.AuctionAllocations.Add(alloc);
        await _dbContext.SaveChangesAsync();

        var payment = new AuctionPayment { AuctionId = auction.Id, PaymentStatus = PaymentStatus.Paid, CustomerProfileId = customerProfile.Id };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var order = new AuctionOrder
        {
            OrderNumber = "FK-FARMER1-001",
            AuctionId = auction.Id,
            AuctionAllocationId = alloc.Id,
            FarmerProfileId = farmerProfile1.Id,
            CustomerProfileId = customerProfile.Id,
            CropId = crop.Id,
            AuctionPaymentId = payment.Id,
            AllocatedQuantityKg = 250,
            PricePerMan = 600,
            TotalAmount = 7500,
            Status = OrderStatus.Confirmed
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act & Assert (Farmer 2 attempting to view Farmer 1's order)
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.GetFarmerOrderDetailsAsync(farmerUser2.Id, order.Id));
    }
}
