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

public sealed class OrderInvoiceAndHistoryTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private NotificationService _notificationService = null!;
    private OrderService _orderService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_InvoiceHistoryTest_{Guid.NewGuid():N}";
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

    private async Task<(ApplicationUser farmerUser, ApplicationUser customerUser, FarmerProfile farmer, CustomerProfile customer, AuctionOrder order, AuctionPayment payment)> SeedPaidOrderAsync(
        decimal allocatedKg = 250m,
        decimal pricePerMan = 600m,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        OrderStatus orderStatus = OrderStatus.Confirmed)
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
            FarmName = "Ramesh Organic Farm",
            FarmLocation = "Surat, Gujarat"
        };
        var customer = new CustomerProfile
        {
            UserId = customerUser.Id,
            FullName = "Archi Customer",
            Phone = "9123456789"
        };
        _dbContext.FarmerProfiles.Add(farmer);
        _dbContext.CustomerProfiles.Add(customer);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id,
            CropName = "Basmati Rice",
            CropType = "Grain",
            Variety = "Super Fine"
        };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var listing = new CropListing
        {
            CropId = crop.Id,
            FarmerProfileId = farmer.Id,
            QuantityForSale = 500m,
            Unit = MeasurementUnit.Kilogram
        };
        _dbContext.CropListings.Add(listing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = 500m,
            MinimumBidIncrement = 10m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-2),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Finalized
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = pricePerMan,
            RequestedQuantityKg = allocatedKg,
            BidStatus = BidStatus.Winning
        };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            BidId = bid.Id,
            CustomerProfileId = customer.Id,
            RequestedQuantityKg = allocatedKg,
            AllocatedQuantityKg = allocatedKg,
            WinningBidAmountPerMan = pricePerMan,
            Status = AllocationStatus.Won
        };
        _dbContext.AuctionAllocations.Add(allocation);
        await _dbContext.SaveChangesAsync();

        var quantityMan = allocatedKg / 20.0m;
        var totalAmount = quantityMan * pricePerMan;

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = totalAmount,
            AllocatedQuantityKg = allocatedKg,
            PaymentStatus = paymentStatus,
            TransactionReference = $"TXN-{Guid.NewGuid():N}[4..10]",
            PaidAtUtc = paymentStatus == PaymentStatus.Paid ? DateTime.UtcNow : null
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var order = new AuctionOrder
        {
            OrderNumber = $"FK-20260815-{_dbContext.AuctionOrders.Count() + 1:D4}",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customer.Id,
            FarmerProfileId = farmer.Id,
            CropId = crop.Id,
            AllocatedQuantityKg = allocatedKg,
            PricePerMan = pricePerMan,
            TotalAmount = totalAmount,
            Status = orderStatus,
            FulfillmentMode = FulfillmentMode.Delivery,
            DeliveryAddress = "123 Ring Road",
            DeliveryCity = "Surat",
            DeliveryState = "Gujarat",
            DeliveryPincode = "395007",
            ContactName = "Archi Customer",
            ContactPhone = "9123456789"
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        return (farmerUser, customerUser, farmer, customer, order, payment);
    }

    [Fact]
    public async Task PaidOrder_GeneratesInvoiceWithUniqueInvoiceNumber()
    {
        var (farmerUser, customerUser, farmer, customer, order, payment) = await SeedPaidOrderAsync(250m, 600m);

        var invoice = await _orderService.GetOrCreateInvoiceForCustomerAsync(customerUser.Id, order.Id);

        Assert.NotNull(invoice);
        Assert.StartsWith("INV-", invoice.InvoiceNumber);
        Assert.Equal(order.Id, invoice.OrderId);
        Assert.Equal("Basmati Rice", invoice.CropName);
        Assert.Equal(250m, invoice.QuantityKg);
        Assert.Equal(12.5m, invoice.QuantityMan);
        Assert.Equal(600m, invoice.PricePerMan);
        Assert.Equal(7500m, invoice.SubtotalAmount);
        Assert.Equal(7500m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.TaxAmount);
        Assert.Equal("PAID", invoice.PaymentStatus);
    }

    [Fact]
    public async Task UnpaidOrder_CannotGenerateInvoice_ThrowsInvalidOperationException()
    {
        var (farmerUser, customerUser, farmer, customer, order, payment) = await SeedPaidOrderAsync(250m, 600m, PaymentStatus.Pending);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.GetOrCreateInvoiceForCustomerAsync(customerUser.Id, order.Id));

        Assert.Contains("Invoice is available after successful payment", ex.Message);
    }

    [Fact]
    public async Task InvoiceGeneration_IsIdempotent_RepeatedRequestsReturnSameInvoice()
    {
        var (farmerUser, customerUser, farmer, customer, order, payment) = await SeedPaidOrderAsync(250m, 600m);

        var invoice1 = await _orderService.GetOrCreateInvoiceForCustomerAsync(customerUser.Id, order.Id);
        var invoice2 = await _orderService.GetOrCreateInvoiceForCustomerAsync(customerUser.Id, order.Id);
        var invoiceFarmer = await _orderService.GetOrCreateInvoiceForFarmerAsync(farmerUser.Id, order.Id);

        Assert.Equal(invoice1.InvoiceId, invoice2.InvoiceId);
        Assert.Equal(invoice1.InvoiceId, invoiceFarmer.InvoiceId);
        Assert.Equal(invoice1.InvoiceNumber, invoice2.InvoiceNumber);

        var dbInvoicesCount = await _dbContext.Invoices.CountAsync(i => i.AuctionOrderId == order.Id);
        Assert.Equal(1, dbInvoicesCount);
    }

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersInvoice()
    {
        var (farmerUser, customerUser1, farmer, customer1, order1, payment1) = await SeedPaidOrderAsync(250m, 600m);

        var otherUser = new ApplicationUser { UserName = "other@test.com", Email = "other@test.com" };
        _dbContext.Users.Add(otherUser);
        await _dbContext.SaveChangesAsync();

        var otherCustomer = new CustomerProfile { UserId = otherUser.Id, FullName = "Other Customer" };
        _dbContext.CustomerProfiles.Add(otherCustomer);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.GetOrCreateInvoiceForCustomerAsync(otherUser.Id, order1.Id));
    }

    [Fact]
    public async Task FarmerCannotAccessAnotherFarmersInvoice()
    {
        var (farmerUser1, customerUser, farmer1, customer, order1, payment1) = await SeedPaidOrderAsync(250m, 600m);

        var otherUser = new ApplicationUser { UserName = "otherfarmer@test.com", Email = "otherfarmer@test.com" };
        _dbContext.Users.Add(otherUser);
        await _dbContext.SaveChangesAsync();

        var otherFarmer = new FarmerProfile { UserId = otherUser.Id, FullName = "Other Farmer" };
        _dbContext.FarmerProfiles.Add(otherFarmer);
        await _dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _orderService.GetOrCreateInvoiceForFarmerAsync(otherUser.Id, order1.Id));
    }

    [Fact]
    public async Task OrderStatusFiltering_ActiveCompletedAndAll_WorksCorrectly()
    {
        var (farmerUser, customerUser, farmer, customer, activeOrder, p1) = await SeedPaidOrderAsync(100m, 500m, PaymentStatus.Paid, OrderStatus.Confirmed);
        var (farmerUser2, customerUser2, farmer2, customer2, completedOrder, p2) = await SeedPaidOrderAsync(200m, 600m, PaymentStatus.Paid, OrderStatus.Completed);

        // Link completedOrder to customerUser as well for status test
        completedOrder.CustomerProfileId = customer.Id;
        await _dbContext.SaveChangesAsync();

        // 1. Filter ALL
        var allOrders = await _orderService.GetCustomerOrdersAsync(customerUser.Id, new CustomerOrderFilterRequest(Status: "ALL"));
        Assert.Equal(2, allOrders.Count);

        // 2. Filter ACTIVE
        var activeOrders = await _orderService.GetCustomerOrdersAsync(customerUser.Id, new CustomerOrderFilterRequest(Status: "ACTIVE"));
        Assert.Single(activeOrders);
        Assert.Equal(activeOrder.Id, activeOrders[0].OrderId);

        // 3. Filter COMPLETED
        var completedOrders = await _orderService.GetCustomerOrdersAsync(customerUser.Id, new CustomerOrderFilterRequest(Status: "COMPLETED"));
        Assert.Single(completedOrders);
        Assert.Equal(completedOrder.Id, completedOrders[0].OrderId);
    }

    [Fact]
    public async Task CompletedOrder_IsImmutable_ModificationThrowsInvalidOperationException()
    {
        var (farmerUser, customerUser, farmer, customer, completedOrder, p) = await SeedPaidOrderAsync(200m, 600m, PaymentStatus.Paid, OrderStatus.Completed);

        // Attempting to update status of completed order
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateOrderStatusAsync(farmerUser.Id, completedOrder.Id, new UpdateOrderStatusRequest("READY_FOR_PICKUP")));

        // Attempting to update fulfillment of completed order
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.UpdateCustomerOrderFulfillmentAsync(customerUser.Id, completedOrder.Id, new UpdateFulfillmentDetailsRequest("DELIVERY", "New Address")));
    }
}
