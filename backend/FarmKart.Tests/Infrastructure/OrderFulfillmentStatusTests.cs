using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Domain.ValueObjects;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public sealed class OrderFulfillmentStatusTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private OrderService _orderService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_OrderFulfillmentTest_{Guid.NewGuid():N}";
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

    private async Task<(ApplicationUser FarmerUser, ApplicationUser CustUser, AuctionOrder Order)> SeedOrderAsync(
        FulfillmentMode mode = FulfillmentMode.Delivery)
    {
        var farmerUser = new ApplicationUser { UserName = $"farmer_{Guid.NewGuid():N}@fk.com", Email = "farmer@fk.com" };
        var custUser = new ApplicationUser { UserName = $"cust_{Guid.NewGuid():N}@fk.com", Email = "cust@fk.com" };
        _dbContext.Users.AddRange(farmerUser, custUser);
        await _dbContext.SaveChangesAsync();

        var farmerProfile = new FarmerProfile
        {
            UserId = farmerUser.Id,
            FullName = "Farmer Ramesh",
            FarmName = "Ramesh Farms",
            FarmLocation = "Surat, Gujarat"
        };
        var custProfile = new CustomerProfile
        {
            UserId = custUser.Id,
            FullName = "Archi Customer",
            Phone = "9876543210",
            AddressInfo = new AddressInfo { AddressLine = "123 Ring Road", City = "Ahmedabad", State = "Gujarat", Pincode = "380001" }
        };
        _dbContext.FarmerProfiles.Add(farmerProfile);
        _dbContext.CustomerProfiles.Add(custProfile);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop { FarmerProfileId = farmerProfile.Id, CropName = "Wheat", CropType = "Grain" };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmerProfile.Id, CropId = crop.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction { CropListingId = listing.Id, FarmerProfileId = farmerProfile.Id, StartingPrice = 500, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow.AddDays(-2), EndTimeUtc = DateTime.UtcNow.AddDays(-1), AuctionStatus = AuctionStatus.Ended };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid { AuctionId = auction.Id, CustomerProfileId = custProfile.Id, Amount = 600, RequestedQuantityKg = 250, BidStatus = BidStatus.Winning };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var alloc = new AuctionAllocation { AuctionId = auction.Id, BidId = bid.Id, CustomerProfileId = custProfile.Id, RequestedQuantityKg = 250, AllocatedQuantityKg = 250, WinningBidAmountPerMan = 600, Status = AllocationStatus.Won };
        _dbContext.AuctionAllocations.Add(alloc);
        await _dbContext.SaveChangesAsync();

        var payment = new AuctionPayment { AuctionId = auction.Id, CustomerProfileId = custProfile.Id, Amount = 7500, PaymentStatus = PaymentStatus.Paid, TransactionReference = "TXN-123" };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var order = new AuctionOrder
        {
            OrderNumber = $"FK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 15),
            AuctionId = auction.Id,
            AuctionAllocationId = alloc.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = custProfile.Id,
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            AllocatedQuantityKg = 250,
            PricePerMan = 600,
            TotalAmount = 7500,
            Status = OrderStatus.Confirmed,
            FulfillmentMode = mode,
            DeliveryAddress = mode == FulfillmentMode.Delivery ? "123 Ring Road" : null,
            DeliveryCity = mode == FulfillmentMode.Delivery ? "Ahmedabad" : null,
            DeliveryState = mode == FulfillmentMode.Delivery ? "Gujarat" : null,
            DeliveryPincode = mode == FulfillmentMode.Delivery ? "380001" : null,
            ContactName = mode == FulfillmentMode.Delivery ? "Archi Customer" : null,
            ContactPhone = mode == FulfillmentMode.Delivery ? "9876543210" : null,
            PickupLocation = mode == FulfillmentMode.Pickup ? "Surat, Gujarat" : null,
            PickupDate = mode == FulfillmentMode.Pickup ? DateTime.UtcNow.AddDays(1) : null
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        return (farmerUser, custUser, order);
    }

    [Fact]
    public async Task DeliveryPath_ValidTransitions_WorkSequentially()
    {
        var (farmerUser, custUser, order) = await SeedOrderAsync(FulfillmentMode.Delivery);

        // 1. Confirmed -> ReadyForPickup
        var res1 = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP", "Ready at warehouse"));
        Assert.Equal("READY_FOR_PICKUP", res1.Status);

        // 2. ReadyForPickup -> Dispatched
        var res2 = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("DISPATCHED", "Out for delivery"));
        Assert.Equal("DISPATCHED", res2.Status);

        // 3. Dispatched -> Delivered
        var res3 = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("DELIVERED", "Handed over to buyer"));
        Assert.Equal("DELIVERED", res3.Status);

        // 4. Delivered -> Completed
        var res4 = await _orderService.UpdateOrderStatusAsync(custUser.Id, order.Id, new UpdateOrderStatusRequest("COMPLETED", "Buyer confirmed receipt"));
        Assert.Equal("COMPLETED", res4.Status);
    }

    [Fact]
    public async Task PickupPath_ValidTransitions_WorkSequentially()
    {
        var (farmerUser, custUser, order) = await SeedOrderAsync(FulfillmentMode.Pickup);

        // 1. Confirmed -> ReadyForPickup
        var res1 = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP", "Ready at farm"));
        Assert.Equal("READY_FOR_PICKUP", res1.Status);

        // 2. ReadyForPickup -> PickedUp
        var res2 = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("PICKED_UP", "Customer picked up crop"));
        Assert.Equal("PICKED_UP", res2.Status);

        // 3. PickedUp -> Completed
        var res3 = await _orderService.UpdateOrderStatusAsync(custUser.Id, order.Id, new UpdateOrderStatusRequest("COMPLETED", "Order complete"));
        Assert.Equal("COMPLETED", res3.Status);
    }

    [Fact]
    public async Task InvalidTransition_ConfirmedToDelivered_ThrowsInvalidOperationException()
    {
        var (farmerUser, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("DELIVERED")));
    }

    [Fact]
    public async Task InvalidCrossModeTransition_DispatchedOnPickupOrder_ThrowsInvalidOperationException()
    {
        var (farmerUser, _, order) = await SeedOrderAsync(FulfillmentMode.Pickup);

        await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("DISPATCHED")));
    }

    [Fact]
    public async Task CompletedOrder_CannotBeModified_ThrowsInvalidOperationException()
    {
        var (farmerUser, custUser, order) = await SeedOrderAsync(FulfillmentMode.Pickup);

        await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP"));
        await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("PICKED_UP"));
        await _orderService.UpdateOrderStatusAsync(custUser.Id, order.Id, new UpdateOrderStatusRequest("COMPLETED"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("CONFIRMED")));
    }

    [Fact]
    public async Task UnauthorizedUser_CannotModifyOrder_ThrowsKeyNotFoundException()
    {
        var (_, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery);
        var randomUserId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.UpdateOrderStatusAsync(randomUserId, order.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP")));
    }

    [Fact]
    public async Task StatusHistory_RecordsTimestampsAndActor()
    {
        var (farmerUser, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery);

        await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP", "Ready note"));

        var histories = await _dbContext.OrderStatusHistories.ToListAsync();
        Assert.NotEmpty(histories);
        var last = histories[^1];
        Assert.Equal(OrderStatus.Confirmed, last.PreviousStatus);
        Assert.Equal(OrderStatus.ReadyForPickup, last.NewStatus);
        Assert.Equal(farmerUser.Id.ToString(), last.ChangedByUserId);
        Assert.Equal("Ready note", last.Note);
    }

    [Fact]
    public async Task DeliveryAddressSnapshot_RemainsStable_WhenProfileChanges()
    {
        var (_, custUser, order) = await SeedOrderAsync(FulfillmentMode.Delivery);

        // Change customer profile address
        var custProfile = await _dbContext.CustomerProfiles.FirstAsync(c => c.UserId == custUser.Id);
        custProfile.AddressInfo.AddressLine = "New 999 Changed St";
        custProfile.AddressInfo.City = "Vadodara";
        await _dbContext.SaveChangesAsync();

        // Query order details
        var details = await _orderService.GetCustomerOrderDetailsAsync(custUser.Id, order.Id);
        Assert.Equal("123 Ring Road", details.DeliveryAddress);
        Assert.Equal("Ahmedabad", details.DeliveryCity);
    }

    [Fact]
    public async Task PastPickupDate_ThrowsArgumentException()
    {
        var (_, custUser, order) = await SeedOrderAsync(FulfillmentMode.Pickup);

        var pastDate = DateTime.UtcNow.AddDays(-1);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _orderService.UpdateCustomerOrderFulfillmentAsync(custUser.Id, order.Id, new UpdateFulfillmentDetailsRequest(
                FulfillmentMode: "PICKUP",
                PickupDate: pastDate
            )));
    }
}
