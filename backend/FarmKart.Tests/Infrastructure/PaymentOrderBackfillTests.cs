using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public sealed class PaymentOrderBackfillTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database=FarmKartDb_BackfillTest_{Guid.NewGuid()};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _dbContext = new FarmKartDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private PaymentOrderBackfillService CreateBackfillService()
    {
        return new PaymentOrderBackfillService(_dbContext, NullLogger<PaymentOrderBackfillService>.Instance);
    }

    private async Task<(FarmerProfile farmer, CustomerProfile customer, CropListing cropListing, Auction auction)> SeedBaseGraphAsync()
    {
        var farmerUser = new ApplicationUser
        {
            UserName = $"farmer_{Guid.NewGuid():N}@test.com",
            Email = $"farmer_{Guid.NewGuid():N}@test.com",
            NormalizedEmail = $"FARMER_{Guid.NewGuid():N}@TEST.COM"
        };
        var customerUser = new ApplicationUser
        {
            UserName = $"customer_{Guid.NewGuid():N}@test.com",
            Email = $"customer_{Guid.NewGuid():N}@test.com",
            NormalizedEmail = $"CUSTOMER_{Guid.NewGuid():N}@TEST.COM"
        };
        _dbContext.Users.AddRange(farmerUser, customerUser);
        await _dbContext.SaveChangesAsync();

        var farmer = new FarmerProfile
        {
            UserId = farmerUser.Id,
            FullName = "Ramesh Farmer",
            Phone = "9876543210"
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
            Variety = "Sharbati",
            Area = 5,
            AreaUnit = FarmSizeUnit.Acre,
            Status = CropStatus.Harvested,
            Quantity = 1000,
            Unit = MeasurementUnit.Kilogram
        };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var cropListing = new CropListing
        {
            FarmerProfileId = farmer.Id,
            Crop = crop,
            QuantityForSale = 1000m,
            Unit = MeasurementUnit.Kilogram,
            ListingType = ListingType.Auction,
            ListingStatus = ListingStatus.Active
        };
        _dbContext.CropListings.Add(cropListing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = cropListing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = 500m,
            CurrentHighestBid = 600m,
            MinimumBidIncrement = 10m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-10),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        return (farmer, customer, cropListing, auction);
    }

    private async Task<AuctionAllocation> SeedAllocationAsync(
        Auction auction,
        CustomerProfile customer,
        decimal requestedKg,
        decimal allocatedKg,
        decimal pricePerMan,
        AllocationStatus status = AllocationStatus.Won)
    {
        var bid = new Bid
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = pricePerMan,
            RequestedQuantityKg = requestedKg,
            BidTimeUtc = DateTime.UtcNow.AddHours(-2),
            BidStatus = BidStatus.Active
        };
        _dbContext.Bids.Add(bid);
        await _dbContext.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            BidId = bid.Id,
            WinningBidAmountPerMan = pricePerMan,
            RequestedQuantityKg = requestedKg,
            AllocatedQuantityKg = allocatedKg,
            Status = status,
            FinalizedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuctionAllocations.Add(allocation);
        await _dbContext.SaveChangesAsync();

        return allocation;
    }

    [Fact]
    public async Task ExecuteBackfill_PaidPaymentWithNoOrder_CreatesOrder()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        var allocation = await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var expectedAmount = Math.Round((250m / 20m) * 600m, 2); // 7500
        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = expectedAmount,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-111",
            PaidAtUtc = DateTime.UtcNow
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal(1, result.ValidForBackfill);
        Assert.Equal("CREATED", result.ItemResults[0].ResultStatus);

        var order = await _dbContext.AuctionOrders.FirstOrDefaultAsync(o => o.AuctionPaymentId == payment.Id);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(250m, order.AllocatedQuantityKg);
        Assert.Equal(600m, order.PricePerMan);
        Assert.Equal(expectedAmount, order.TotalAmount);
        Assert.Equal(customer.Id, order.CustomerProfileId);
        Assert.Equal(farmer.Id, order.FarmerProfileId);
        Assert.Equal(listing.CropId, order.CropId);
    }

    [Fact]
    public async Task ExecuteBackfill_PaidPaymentWithExistingOrder_DoesNotCreateDuplicate()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        var allocation = await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-EXISTING",
            PaidAtUtc = DateTime.UtcNow
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var existingOrder = new AuctionOrder
        {
            OrderNumber = "FK-20260815-0001",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customer.Id,
            FarmerProfileId = farmer.Id,
            CropId = listing.CropId,
            AllocatedQuantityKg = 250m,
            PricePerMan = 600m,
            TotalAmount = 7500m,
            Status = OrderStatus.Confirmed
        };
        _dbContext.AuctionOrders.Add(existingOrder);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(0, result.OrdersCreated);
        Assert.Equal(1, result.AlreadyHaveOrders);
        Assert.Equal("SKIPPED_ALREADY_EXISTS", result.ItemResults[0].ResultStatus);
        Assert.Equal(1, await _dbContext.AuctionOrders.CountAsync());
    }

    [Fact]
    public async Task ExecuteBackfill_PendingPayment_IsSkipped()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Pending,
            TransactionReference = "TXN-PENDING"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(0, result.TotalPaidPaymentsFound);
        Assert.Equal(0, result.OrdersCreated);
        Assert.Equal(0, await _dbContext.AuctionOrders.CountAsync());
    }

    [Fact]
    public async Task ExecuteBackfill_FailedPayment_IsSkipped()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Failed,
            TransactionReference = "TXN-FAILED"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(0, result.TotalPaidPaymentsFound);
        Assert.Equal(0, result.OrdersCreated);
    }

    [Fact]
    public async Task ExecuteBackfill_CancelledPayment_IsSkipped()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Failed,
            TransactionReference = "TXN-CANCELLED"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(0, result.TotalPaidPaymentsFound);
        Assert.Equal(0, result.OrdersCreated);
    }

    [Fact]
    public async Task ExecuteBackfill_LostAllocation_IsSkipped()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 300m, 0m, 600m, AllocationStatus.Lost);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-LOST"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.TotalPaidPaymentsFound);
        Assert.Equal(0, result.OrdersCreated);
        Assert.Equal("SKIPPED_INVALID_ALLOCATION", result.ItemResults[0].ResultStatus);
    }

    [Fact]
    public async Task ExecuteBackfill_WonAllocation_CreatesOrder()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 200m, 200m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 6000m, // (200/20)*600 = 6000
            PaymentMethod = PaymentMethod.Card,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-WON"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal("CREATED", result.ItemResults[0].ResultStatus);
    }

    [Fact]
    public async Task ExecuteBackfill_PartiallyWonAllocation_CreatesOrder()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 150m, 150m, 600m, AllocationStatus.PartiallyWon);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 4500m, // (150/20)*600 = 4500
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-PARTIAL"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal("CREATED", result.ItemResults[0].ResultStatus);
    }

    [Fact]
    public async Task ExecuteBackfill_AllocatedQuantityIsUsed_NotRequestedQuantity()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();

        // Requested 300 Kg, Allocated 250 Kg
        await SeedAllocationAsync(auction, customer, 300m, 250m, 600m, AllocationStatus.PartiallyWon);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m, // (250/20)*600 = 7500
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-QTY-CHECK"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.OrdersCreated);
        var order = await _dbContext.AuctionOrders.FirstAsync();
        Assert.Equal(250m, order.AllocatedQuantityKg); // MUST BE 250, NOT 300
        Assert.NotEqual(300m, order.AllocatedQuantityKg);
    }

    [Fact]
    public async Task ExecuteBackfill_CorrectManPriceUsed()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 100m, 100m, 800m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 4000m, // 5 * 800 = 4000
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-MAN-PRICE"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.OrdersCreated);
        var order = await _dbContext.AuctionOrders.FirstAsync();
        Assert.Equal(800m, order.PricePerMan);
        Assert.Equal(4000m, order.TotalAmount);
    }

    [Fact]
    public async Task ExecuteBackfill_CorrectPaymentAmountValidated()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 60m, 60m, 500m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 1500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-EXACT"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal("CREATED", result.ItemResults[0].ResultStatus);
    }

    [Fact]
    public async Task ExecuteBackfill_AmountMismatch_IsSkipped()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 5000m, // MISMATCH! (Payment says 5000 instead of 7500)
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-MISMATCH"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.TotalPaidPaymentsFound);
        Assert.Equal(0, result.OrdersCreated);
        Assert.Equal("SKIPPED_AMOUNT_MISMATCH", result.ItemResults[0].ResultStatus);
        Assert.Equal(0, await _dbContext.AuctionOrders.CountAsync());
    }

    [Fact]
    public async Task ExecuteBackfill_ExistingPaymentRemainsUnchanged()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-UNMUTATED",
            PaidAtUtc = DateTime.UtcNow
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        await service.ExecuteBackfillAsync(dryRun: false);

        var unmutatedPayment = await _dbContext.AuctionPayments.FindAsync(payment.Id);
        Assert.NotNull(unmutatedPayment);
        Assert.Equal(PaymentStatus.Paid, unmutatedPayment.PaymentStatus);
        Assert.Equal(7500m, unmutatedPayment.Amount);
        Assert.Equal("TXN-UNMUTATED", unmutatedPayment.TransactionReference);
    }

    [Fact]
    public async Task ExecuteBackfill_ExistingOrderRemainsUnchanged()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        var allocation = await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-UNCHANGED"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var preExistingOrder = new AuctionOrder
        {
            OrderNumber = "FK-20260815-9999",
            AuctionId = auction.Id,
            AuctionAllocationId = allocation.Id,
            AuctionPaymentId = payment.Id,
            CustomerProfileId = customer.Id,
            FarmerProfileId = farmer.Id,
            CropId = listing.CropId,
            AllocatedQuantityKg = 250m,
            PricePerMan = 600m,
            TotalAmount = 7500m,
            Status = OrderStatus.Confirmed
        };
        _dbContext.AuctionOrders.Add(preExistingOrder);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        await service.ExecuteBackfillAsync(dryRun: false);

        var queriedOrder = await _dbContext.AuctionOrders.FindAsync(preExistingOrder.Id);
        Assert.NotNull(queriedOrder);
        Assert.Equal("FK-20260815-9999", queriedOrder.OrderNumber);
    }

    [Fact]
    public async Task ExecuteBackfill_MultiplePayments_CreatesMultipleOrders()
    {
        var (farmer, customer1, listing, auction) = await SeedBaseGraphAsync();

        var customer2User = new ApplicationUser { UserName = $"cust2_{Guid.NewGuid():N}@test.com", Email = $"cust2_{Guid.NewGuid():N}@test.com" };
        _dbContext.Users.Add(customer2User);
        await _dbContext.SaveChangesAsync();

        var customer2 = new CustomerProfile { UserId = customer2User.Id, FullName = "Cust 2", Phone = "9999999999" };
        _dbContext.CustomerProfiles.Add(customer2);
        await _dbContext.SaveChangesAsync();

        await SeedAllocationAsync(auction, customer1, 250m, 250m, 600m, AllocationStatus.Won);
        await SeedAllocationAsync(auction, customer2, 100m, 100m, 600m, AllocationStatus.Won);

        var pay1 = new AuctionPayment { AuctionId = auction.Id, CustomerProfileId = customer1.Id, Amount = 7500m, PaymentMethod = PaymentMethod.Upi, PaymentStatus = PaymentStatus.Paid, TransactionReference = "TXN-M1" };
        var pay2 = new AuctionPayment { AuctionId = auction.Id, CustomerProfileId = customer2.Id, Amount = 3000m, PaymentMethod = PaymentMethod.Card, PaymentStatus = PaymentStatus.Paid, TransactionReference = "TXN-M2" };
        _dbContext.AuctionPayments.AddRange(pay1, pay2);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(2, result.OrdersCreated);
        Assert.Equal(2, await _dbContext.AuctionOrders.CountAsync());
    }

    [Fact]
    public async Task ExecuteBackfill_IsIdempotent_SecondRunCreatesZeroOrders()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-IDEMPOTENT"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();

        // RUN 1
        var run1 = await service.ExecuteBackfillAsync(dryRun: false);
        Assert.Equal(1, run1.OrdersCreated);
        Assert.Equal(1, await _dbContext.AuctionOrders.CountAsync());

        // RUN 2
        var run2 = await service.ExecuteBackfillAsync(dryRun: false);
        Assert.Equal(0, run2.OrdersCreated);
        Assert.Equal(1, run2.AlreadyHaveOrders);
        Assert.Equal(1, await _dbContext.AuctionOrders.CountAsync());
    }

    [Fact]
    public async Task ExecuteBackfill_MissingRelationship_IsHandledSafely()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-ORPHAN"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        var result = await service.ExecuteBackfillAsync(dryRun: false);

        Assert.Equal(1, result.TotalPaidPaymentsFound);
        Assert.Equal(0, result.OrdersCreated);
        Assert.Equal("SKIPPED_INVALID_ALLOCATION", result.ItemResults[0].ResultStatus);
    }

    [Fact]
    public async Task ExecuteBackfill_DryRunMode_DoesNotModifyDatabase()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-DRYRUN"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();

        // DRY RUN = TRUE
        var result = await service.ExecuteBackfillAsync(dryRun: true);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.ValidForBackfill);
        Assert.Equal(0, result.OrdersCreated);
        Assert.Equal("DRY_RUN_ELIGIBLE", result.ItemResults[0].ResultStatus);

        // Verify DB is clean
        Assert.Equal(0, await _dbContext.AuctionOrders.CountAsync());
    }

    [Fact]
    public async Task ExecuteBackfill_CustomerAndFarmerRelationshipsAreCorrect()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-REL-CHECK"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var service = CreateBackfillService();
        await service.ExecuteBackfillAsync(dryRun: false);

        var order = await _dbContext.AuctionOrders.FirstAsync();
        Assert.Equal(customer.Id, order.CustomerProfileId);
        Assert.Equal(farmer.Id, order.FarmerProfileId);
        Assert.Equal(listing.CropId, order.CropId);
    }

    [Fact]
    public async Task ExecuteBackfill_BackfilledOrderAppearsInCustomerOrderService()
    {
        var (farmer, customer, listing, auction) = await SeedBaseGraphAsync();
        await SeedAllocationAsync(auction, customer, 250m, 250m, 600m, AllocationStatus.Won);

        var payment = new AuctionPayment
        {
            AuctionId = auction.Id,
            CustomerProfileId = customer.Id,
            Amount = 7500m,
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Paid,
            TransactionReference = "TXN-MYORDERS-CHECK"
        };
        _dbContext.AuctionPayments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var backfillService = CreateBackfillService();
        await backfillService.ExecuteBackfillAsync(dryRun: false);

        var orderService = new OrderService(_dbContext, new NotificationService(_dbContext));
        var orders = await orderService.GetCustomerOrdersAsync(customer.UserId, new CustomerOrderFilterRequest());

        Assert.Single(orders);
        Assert.Equal("Wheat", orders[0].CropName);
        Assert.Equal(250m, orders[0].AllocatedQuantityKg);
        Assert.Equal(12.5m, orders[0].AllocatedQuantityMan);
        Assert.Equal(600m, orders[0].PricePerMan);
        Assert.Equal(7500m, orders[0].TotalAmount);
        Assert.Equal("CONFIRMED", orders[0].Status);
    }
}
