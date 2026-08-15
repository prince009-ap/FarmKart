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

public sealed class OrderHandoverTrackingTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private OrderService _orderService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_HandoverTrackingTest_{Guid.NewGuid():N}";
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

    private async Task<(ApplicationUser FarmerUser, ApplicationUser CustUser, FarmerProfile FarmerProfile, CustomerProfile CustProfile, AuctionOrder Order)> SeedOrderAsync(
        FulfillmentMode mode = FulfillmentMode.Delivery,
        OrderStatus status = OrderStatus.ReadyForPickup)
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

        var crop = new Crop
        {
            FarmerProfileId = farmerProfile.Id,
            CropName = "Organic Rice",
            CropType = "Grain",
            Variety = "Basmati",
            Unit = MeasurementUnit.Kilogram,
            Quantity = 1000
        };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing
        {
            CropId = crop.Id,
            FarmerProfileId = farmerProfile.Id,
            QuantityForSale = 500,
            Unit = MeasurementUnit.Kilogram,
            PricePerUnit = 40,
            ListingType = ListingType.Auction,
            ListingStatus = ListingStatus.Active
        };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmerProfile.Id,
            StartingPrice = 40,
            MinimumBidIncrement = 1,
            StartTimeUtc = DateTime.UtcNow.AddDays(-1),
            EndTimeUtc = DateTime.UtcNow.AddDays(1),
            AuctionStatus = AuctionStatus.Live
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = custProfile.Id,
            Amount = 45,
            RequestedQuantityKg = 200,
            BidStatus = BidStatus.Winning,
            BidTimeUtc = DateTime.UtcNow.AddHours(-2)
        };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            CustomerProfileId = custProfile.Id,
            BidId = bid.Id,
            RequestedQuantityKg = 200,
            AllocatedQuantityKg = 200,
            WinningBidAmountPerMan = 900,
            Status = AllocationStatus.Won,
            FinalizedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuctionAllocations.Add(allocation);
        await _dbContext.SaveChangesAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = custProfile.Id,
            Amount = 9000,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "FK-TEST-REF-001",
            PaidAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var order = new AuctionOrder
        {
            OrderNumber = "FK-20260815-9999",
            AuctionId = auction.Id,
            CropId = crop.Id,
            FarmerProfileId = farmerProfile.Id,
            CustomerProfileId = custProfile.Id,
            AuctionPaymentId = payment.Id,
            AuctionAllocationId = allocation.Id,
            AllocatedQuantityKg = 200,
            PricePerMan = 900,
            TotalAmount = 9000,
            Status = status,
            FulfillmentMode = mode,
            DeliveryAddress = mode == FulfillmentMode.Delivery ? "123 Ring Road" : null,
            DeliveryCity = mode == FulfillmentMode.Delivery ? "Ahmedabad" : null,
            DeliveryState = mode == FulfillmentMode.Delivery ? "Gujarat" : null,
            DeliveryPincode = mode == FulfillmentMode.Delivery ? "380001" : null,
            ContactName = mode == FulfillmentMode.Delivery ? "Archi Customer" : null,
            ContactPhone = mode == FulfillmentMode.Delivery ? "9876543210" : null,
            PickupLocation = mode == FulfillmentMode.Pickup ? "Surat, Gujarat Farm" : null,
            PickupDate = mode == FulfillmentMode.Pickup ? DateTime.UtcNow.AddDays(2) : null,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        return (farmerUser, custUser, farmerProfile, custProfile, order);
    }

    [Fact]
    public async Task Farmer_CanMarkOwnPickupOrder_AsPickedUp()
    {
        var (farmerUser, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Pickup, OrderStatus.ReadyForPickup);

        var request = new UpdateOrderStatusRequest("PICKED_UP", "Customer collected the rice");
        var updated = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, request);

        Assert.Equal("PICKED_UP", updated.Status);

        var dbOrder = await _dbContext.AuctionOrders.Include(o => o.StatusHistories).FirstAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.PickedUp, dbOrder.Status);
        Assert.Single(dbOrder.StatusHistories);
        var historyList = new List<OrderStatusHistory>(dbOrder.StatusHistories);
        Assert.Equal("Customer collected the rice", historyList[0].Note);
    }

    [Fact]
    public async Task Farmer_CanDispatchOwnDeliveryOrder()
    {
        var (farmerUser, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.ReadyForPickup);

        var request = new UpdateOrderStatusRequest("DISPATCHED", "Dispatched through local delivery truck");
        var updated = await _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, request);

        Assert.Equal("DISPATCHED", updated.Status);

        var dbOrder = await _dbContext.AuctionOrders.Include(o => o.StatusHistories).FirstAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Dispatched, dbOrder.Status);
        Assert.Single(dbOrder.StatusHistories);
        var historyList = new List<OrderStatusHistory>(dbOrder.StatusHistories);
        Assert.Equal(farmerUser.Id.ToString(), historyList[0].ChangedByUserId);
    }

    [Fact]
    public async Task Farmer_CannotModifyAnotherFarmersOrder_ThrowsKeyNotFoundException()
    {
        var (_, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.ReadyForPickup);

        var otherFarmer = new ApplicationUser { UserName = $"other_farmer_{Guid.NewGuid():N}@fk.com", Email = "other@fk.com" };
        _dbContext.Users.Add(otherFarmer);
        await _dbContext.SaveChangesAsync();

        var otherProfile = new FarmerProfile { UserId = otherFarmer.Id, FullName = "Other Farmer" };
        _dbContext.FarmerProfiles.Add(otherProfile);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateOrderStatusRequest("DISPATCHED", "Attempt dispatch");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.UpdateOrderStatusAsync(otherFarmer.Id, order.Id, request));
    }

    [Fact]
    public async Task Customer_CannotPerformFarmerFulfillmentActions_ThrowsUnauthorizedAccessException()
    {
        var (_, custUser, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.ReadyForPickup);

        var request = new UpdateOrderStatusRequest("DISPATCHED", "Customer attempting dispatch");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _orderService.UpdateOrderStatusAsync(custUser.Id, order.Id, request));
    }

    [Fact]
    public async Task ConfirmedToPickedUp_Directly_ThrowsInvalidOperationException()
    {
        var (farmerUser, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Pickup, OrderStatus.Confirmed);

        var request = new UpdateOrderStatusRequest("PICKED_UP", "Skip ready for pickup");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, request));
    }

    [Fact]
    public async Task ConfirmedToDispatched_Directly_ThrowsInvalidOperationException()
    {
        var (farmerUser, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.Confirmed);

        var request = new UpdateOrderStatusRequest("DISPATCHED", "Skip ready for pickup");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, request));
    }

    [Fact]
    public async Task WrongFulfillmentStatusCombo_DispatchedOnPickupOrder_ThrowsInvalidOperationException()
    {
        var (farmerUser, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Pickup, OrderStatus.ReadyForPickup);

        var request = new UpdateOrderStatusRequest("DISPATCHED", "Wrong mode dispatch");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, order.Id, request));
    }

    [Fact]
    public async Task Customer_CanRetrieveOwnTracking_ReturnsDetailsAndStatusHistory()
    {
        var (_, custUser, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.ReadyForPickup);

        var tracking = await _orderService.GetCustomerOrderTrackingAsync(custUser.Id, order.Id);

        Assert.Equal(order.OrderNumber, tracking.OrderNumber);
        Assert.Equal("Organic Rice", tracking.CropName);
        Assert.Equal("DELIVERY", tracking.FulfillmentMode);
        Assert.Equal("READY_FOR_PICKUP", tracking.CurrentStatus);
        Assert.Equal("Your order is ready for pickup/dispatch.", tracking.StatusMessage);
        Assert.Equal("Ahmedabad", tracking.DeliveryCity);
    }

    [Fact]
    public async Task Customer_CannotRetrieveAnotherCustomersTracking_ThrowsKeyNotFoundException()
    {
        var (_, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.ReadyForPickup);

        var otherCustUser = new ApplicationUser { UserName = $"other_cust_{Guid.NewGuid():N}@fk.com", Email = "otherc@fk.com" };
        _dbContext.Users.Add(otherCustUser);
        await _dbContext.SaveChangesAsync();

        var otherCustProfile = new CustomerProfile { UserId = otherCustUser.Id, FullName = "Other Customer" };
        _dbContext.CustomerProfiles.Add(otherCustProfile);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.GetCustomerOrderTrackingAsync(otherCustUser.Id, order.Id));
    }

    [Fact]
    public async Task UnauthenticatedRequest_ThrowsUnauthorizedAccessException()
    {
        var (_, _, _, _, order) = await SeedOrderAsync(FulfillmentMode.Delivery, OrderStatus.ReadyForPickup);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _orderService.GetCustomerOrderTrackingAsync(Guid.NewGuid(), order.Id));
    }
}
