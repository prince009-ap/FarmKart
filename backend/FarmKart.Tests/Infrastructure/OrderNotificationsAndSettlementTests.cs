using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class OrderNotificationsAndSettlementTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private NotificationService _notificationService = null!;
    private OrderService _orderService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_NotificationsSettlementTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _dbContext = new FarmKartDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _notificationService = new NotificationService(_dbContext);
        _orderService = new OrderService(_dbContext, _notificationService);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task<(ApplicationUser farmerUser, ApplicationUser customerUser, FarmerProfile farmer, CustomerProfile customer, Crop crop, CropListing listing, Auction auction, AuctionAllocation allocation, AuctionPayment payment)> SeedOrderGraphAsync(decimal cropStockKg = 500m, decimal auctionQtyKg = 500m, decimal allocatedKg = 250m, decimal bidPricePerMan = 600m)
    {
        var farmerUser = new ApplicationUser
        {
            UserName = $"farmer_{Guid.NewGuid():N}@test.com",
            Email = $"farmer_{Guid.NewGuid():N}@test.com"
        };
        var customerUser = new ApplicationUser
        {
            UserName = $"customer_{Guid.NewGuid():N}@test.com",
            Email = $"customer_{Guid.NewGuid():N}@test.com"
        };
        _dbContext.Users.AddRange(farmerUser, customerUser);
        await _dbContext.SaveChangesAsync();

        var farmer = new FarmerProfile
        {
            UserId = farmerUser.Id,
            FullName = "Ramesh Farmer",
            Phone = "9876543210",
            FarmLocation = "Punjab, India"
        };
        var customer = new CustomerProfile
        {
            UserId = customerUser.Id,
            FullName = "Priya Customer",
            Phone = "9123456789"
        };
        _dbContext.FarmerProfiles.Add(farmer);
        _dbContext.CustomerProfiles.Add(customer);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id,
            CropName = "Wheat",
            CropType = "Grain",
            Quantity = cropStockKg,
            Unit = MeasurementUnit.Kilogram,
            Status = CropStatus.Harvested
        };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing
        {
            CropId = crop.Id,
            FarmerProfileId = farmer.Id,
            QuantityForSale = auctionQtyKg,
            Unit = MeasurementUnit.Kilogram,
            PricePerUnit = 500m
        };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = 50m,
            CurrentHighestBid = bidPricePerMan,
            MinimumBidIncrement = 5m,
            StartTimeUtc = now.AddHours(-2),
            EndTimeUtc = now.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = bidPricePerMan,
            RequestedQuantityKg = allocatedKg,
            BidTimeUtc = now.AddHours(-1.5),
            BidStatus = BidStatus.Winning
        };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            BidId = bid.Id,
            WinningBidAmountPerMan = bidPricePerMan,
            RequestedQuantityKg = allocatedKg,
            AllocatedQuantityKg = allocatedKg,
            Status = AllocationStatus.Won,
            FinalizedAtUtc = now.AddHours(-1)
        };
        _dbContext.AuctionAllocations.Add(allocation);
        await _dbContext.SaveChangesAsync();

        var expectedAmount = Math.Round((allocatedKg / 20m) * bidPricePerMan, 2);
        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = expectedAmount,
            AllocatedQuantityKg = allocatedKg,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-NOTIF-SETTLE-001",
            PaidAtUtc = now
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        return (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment);
    }

    [Fact]
    public async Task CreateOrderFromPaidPaymentAsync_CreatesCustomerAndFarmerNotificationsAndSettlesStock()
    {
        // Arrange
        var (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment) = await SeedOrderGraphAsync(500m, 500m, 250m, 600m);

        // Act
        var orderResponse = await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        // Assert - Order created
        Assert.NotNull(orderResponse);
        Assert.StartsWith("FK-", orderResponse.OrderNumber);

        // Assert - Notifications created
        var customerNotifs = await _notificationService.GetNotificationsAsync(customerUser.Id);
        var farmerNotifs = await _notificationService.GetNotificationsAsync(farmerUser.Id);

        Assert.Single(customerNotifs);
        Assert.Equal("Order Confirmed", customerNotifs[0].Title);
        Assert.Contains(orderResponse.OrderNumber, customerNotifs[0].Message);
        Assert.Equal(orderResponse.OrderId, customerNotifs[0].RelatedOrderId);

        Assert.True(farmerNotifs.Count >= 1);
        Assert.Contains(farmerNotifs, n => n.Title == "Order Paid & Confirmed" && n.RelatedOrderId == orderResponse.OrderId);

        // Assert - Stock Settlement (500 Kg - 250 Kg = 250 Kg remaining)
        var updatedCrop = await _dbContext.Crops.FirstAsync(c => c.Id == crop.Id);
        Assert.Equal(250m, updatedCrop.Quantity);

        var settlement = await _dbContext.OrderSettlements.FirstOrDefaultAsync(s => s.AuctionOrderId == orderResponse.OrderId);
        Assert.NotNull(settlement);
        Assert.Equal(250m, settlement.SettledQuantityKg);
        Assert.Equal("SETTLED", settlement.SettlementStatus);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_TriggersCorrectNotificationsForCustomerAndFarmer()
    {
        // Arrange
        var (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment) = await SeedOrderGraphAsync();
        var orderResponse = await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        // Act - Farmer updates status to READY_FOR_PICKUP
        await _orderService.UpdateOrderStatusAsync(farmerUser.Id, orderResponse.OrderId, new UpdateOrderStatusRequest("READY_FOR_PICKUP", "Ready at farm gate"));

        // Assert Notifications
        var customerNotifs = await _notificationService.GetNotificationsAsync(customerUser.Id);
        Assert.Contains(customerNotifs, n => n.Title == "Order Ready for Pickup" && n.NotificationType == "OrderReadyForPickup");

        var farmerNotifs = await _notificationService.GetNotificationsAsync(farmerUser.Id);
        Assert.Contains(farmerNotifs, n => n.Title == "Order Ready for Fulfillment" && n.NotificationType == "OrderReadyForPickup");
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_DuplicateStatusRequest_DoesNotCreateDuplicateNotification()
    {
        // Arrange
        var (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment) = await SeedOrderGraphAsync();
        var orderResponse = await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        // Act - Call READY_FOR_PICKUP status transition
        await _orderService.UpdateOrderStatusAsync(farmerUser.Id, orderResponse.OrderId, new UpdateOrderStatusRequest("READY_FOR_PICKUP"));

        var initialCustomerNotifs = await _notificationService.GetNotificationsAsync(customerUser.Id);
        var readyCountInitial = initialCustomerNotifs.Count(n => n.NotificationType == "OrderReadyForPickup");
        Assert.Equal(1, readyCountInitial);

        // Attempt duplicate notification creation manually
        await _notificationService.CreateNotificationAsync(
            customerUser.Id.ToString(),
            "Order Ready for Pickup",
            $"Your order #{orderResponse.OrderNumber} is ready for pickup.",
            NotificationType.OrderReadyForPickup,
            relatedOrderId: orderResponse.OrderId,
            relatedAuctionId: auction.Id);

        var finalCustomerNotifs = await _notificationService.GetNotificationsAsync(customerUser.Id);
        var readyCountFinal = finalCustomerNotifs.Count(n => n.NotificationType == "OrderReadyForPickup");

        // Assert - Idempotency preserved (no duplicate notification created)
        Assert.Equal(1, readyCountFinal);
    }

    [Fact]
    public async Task NotificationSecurity_UserOnlyReceivesOwnNotifications()
    {
        // Arrange
        var (farmerUser, customerUser, _, _, _, _, _, _, payment) = await SeedOrderGraphAsync();
        await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        var unauthorizedUser = new ApplicationUser
        {
            UserName = $"hacker_{Guid.NewGuid():N}@test.com",
            Email = $"hacker_{Guid.NewGuid():N}@test.com"
        };
        _dbContext.Users.Add(unauthorizedUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var customerNotifs = await _notificationService.GetNotificationsAsync(customerUser.Id);
        var unauthorizedNotifs = await _notificationService.GetNotificationsAsync(unauthorizedUser.Id);

        // Assert
        Assert.NotEmpty(customerNotifs);
        Assert.Empty(unauthorizedNotifs);
    }

    [Fact]
    public async Task NotificationReadOperations_MarkAsReadAndUnreadCount()
    {
        // Arrange
        var (farmerUser, customerUser, _, _, _, _, _, _, payment) = await SeedOrderGraphAsync();
        await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        var unreadBefore = await _notificationService.GetUnreadCountAsync(customerUser.Id);
        Assert.Equal(1, unreadBefore.UnreadCount);

        var notifs = await _notificationService.GetNotificationsAsync(customerUser.Id);
        var targetNotif = notifs[0];

        // Act - Mark single notification as read
        var readNotif = await _notificationService.MarkAsReadAsync(customerUser.Id, targetNotif.Id);
        Assert.True(readNotif.IsRead);

        var unreadAfter = await _notificationService.GetUnreadCountAsync(customerUser.Id);
        Assert.Equal(0, unreadAfter.UnreadCount);
    }

    [Fact]
    public async Task Settlement_PartialAllocation_SettlesOnlyAllocatedQuantity()
    {
        // Arrange: Auction for 500 Kg, allocation is only 300 Kg, original stock 500 Kg
        var (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment) = await SeedOrderGraphAsync(cropStockKg: 500m, auctionQtyKg: 500m, allocatedKg: 300m);

        // Act
        var orderResponse = await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        // Assert - Exactly 300 Kg deducted from 500 Kg stock, leaving 200 Kg
        var updatedCrop = await _dbContext.Crops.FirstAsync(c => c.Id == crop.Id);
        Assert.Equal(200m, updatedCrop.Quantity);

        var settlement = await _dbContext.OrderSettlements.FirstAsync(s => s.AuctionOrderId == orderResponse.OrderId);
        Assert.Equal(300m, settlement.SettledQuantityKg);
        Assert.Equal("SETTLED", settlement.SettlementStatus);
    }

    [Fact]
    public async Task Settlement_Idempotent_SecondSettlementCallDoesNotDoubleSubtractStock()
    {
        // Arrange
        var (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment) = await SeedOrderGraphAsync(500m, 500m, 200m);
        var orderResponse = await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        var cropAfterFirstSettle = await _dbContext.Crops.FirstAsync(c => c.Id == crop.Id);
        Assert.Equal(300m, cropAfterFirstSettle.Quantity); // 500 - 200 = 300

        // Act - Second explicit settlement call for the same order
        var secondSettlement = await _orderService.SettleOrderAsync(farmerUser.Id, orderResponse.OrderId);

        // Assert - Stock remains 300 Kg (NOT 100 Kg)
        var cropAfterSecondSettle = await _dbContext.Crops.FirstAsync(c => c.Id == crop.Id);
        Assert.Equal(300m, cropAfterSecondSettle.Quantity);
        Assert.Equal(200m, secondSettlement.SettledQuantityKg);
    }

    [Fact]
    public async Task Settlement_InsufficientStock_CapsDeductionAtAvailableStock()
    {
        // Arrange - Crop stock is 100 Kg, but order demands 250 Kg
        var (farmerUser, customerUser, farmer, customer, crop, listing, auction, allocation, payment) = await SeedOrderGraphAsync(cropStockKg: 100m, auctionQtyKg: 500m, allocatedKg: 250m);

        // Act
        var order = await _orderService.CreateOrderFromPaidPaymentAsync(payment.Id);

        // Verify stock was safely capped at 0 Kg (never negative stock)
        var intactCrop = await _dbContext.Crops.FirstAsync(c => c.Id == crop.Id);
        Assert.Equal(0m, intactCrop.Quantity);
        Assert.True(order.OrderId != Guid.Empty);
    }
}
