using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public sealed class OrderReviewTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private INotificationService _notificationService = null!;
    private OrderReviewService _reviewService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_ReviewTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _dbContext = new FarmKartDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _notificationService = new NotificationService(_dbContext);
        _reviewService = new OrderReviewService(_dbContext, _notificationService);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task<(ApplicationUser farmerUser, ApplicationUser customerUser, FarmerProfile farmer, CustomerProfile customer, AuctionOrder order)> SeedOrderAsync(
        OrderStatus orderStatus = OrderStatus.Completed,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        decimal allocatedKg = 250m,
        decimal pricePerMan = 600m)
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
            TransactionReference = $"TXN-{Guid.NewGuid():N}",
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
            CreatedAtUtc = DateTime.UtcNow.AddHours(-4)
        };
        _dbContext.AuctionOrders.Add(order);
        await _dbContext.SaveChangesAsync();

        return (farmerUser, customerUser, farmer, customer, order);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_CompletedPaidOrder_CreatesReviewSuccessfully()
    {
        var (farmerUser, customerUser, farmer, customer, order) = await SeedOrderAsync(OrderStatus.Completed, PaymentStatus.Paid);

        var request = new CreateOrderReviewRequest(5, "Good quality wheat and smooth transaction.");
        var response = await _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, request);

        Assert.NotNull(response);
        Assert.Equal(order.Id, response.OrderId);
        Assert.Equal(5, response.Rating);
        Assert.Equal("Good quality wheat and smooth transaction.", response.Comment);
        Assert.Equal("Archi Customer", response.CustomerName);
        Assert.Equal("Ramesh Farmer", response.FarmerName);
        Assert.Equal("Basmati Rice", response.CropName);

        var dbReview = await _dbContext.Reviews.FirstOrDefaultAsync(r => r.RelatedEntityId == order.Id);
        Assert.NotNull(dbReview);
        Assert.Equal(customerUser.Id.ToString(), dbReview.ReviewerUserId);
        Assert.Equal(farmerUser.Id.ToString(), dbReview.RevieweeUserId);
        Assert.Equal(ReviewEntityType.Order, dbReview.RelatedEntityType);

        var notification = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.RecipientUserId == farmerUser.Id.ToString());
        Assert.NotNull(notification);
        Assert.Equal("New Review Received", notification.Title);
        Assert.Equal(NotificationType.Review, notification.NotificationType);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_NonCompletedOrder_ThrowsInvalidOperationException()
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync(OrderStatus.Confirmed, PaymentStatus.Paid);

        var request = new CreateOrderReviewRequest(5, "Great product.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, request));

        Assert.Contains("Only completed orders can be reviewed", ex.Message);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_UnpaidOrder_ThrowsInvalidOperationException()
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync(OrderStatus.Completed, PaymentStatus.Pending);

        var request = new CreateOrderReviewRequest(5, "Great product.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, request));

        Assert.Contains("Order must be paid before reviewing", ex.Message);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_OtherCustomerOrder_ThrowsInvalidOperationException()
    {
        var (_, _, _, _, order) = await SeedOrderAsync();
        var otherUserId = Guid.NewGuid();

        var otherCustUser = new ApplicationUser
        {
            Id = otherUserId,
            UserName = $"other_{otherUserId:N}@test.com",
            Email = $"other_{otherUserId:N}@test.com"
        };
        var otherCustProfile = new CustomerProfile
        {
            UserId = otherUserId,
            FullName = "Other Customer",
            Phone = "9998887776"
        };
        _dbContext.Users.Add(otherCustUser);
        _dbContext.CustomerProfiles.Add(otherCustProfile);
        await _dbContext.SaveChangesAsync();

        var request = new CreateOrderReviewRequest(5, "Great product.");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.CreateOrderReviewAsync(otherUserId.ToString(), order.Id, request));

        Assert.Contains("You can only review your own orders", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task CreateOrderReviewAsync_ValidRating_Succeeds(int validRating)
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync();

        var response = await _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(validRating, null));
        Assert.Equal(validRating, response.Rating);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task CreateOrderReviewAsync_InvalidRating_ThrowsArgumentException(int invalidRating)
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(invalidRating, null)));

        Assert.Contains("Rating must be between 1 and 5 stars", ex.Message);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_DuplicateReview_ThrowsInvalidOperationException()
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync();

        await _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(5, "First review"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(4, "Second review")));

        Assert.Contains("already been submitted for this order", ex.Message);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_WithoutComment_Succeeds()
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync();

        var response = await _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(4, null));

        Assert.Equal(4, response.Rating);
        Assert.Null(response.Comment);
    }

    [Fact]
    public async Task CreateOrderReviewAsync_ExcessivelyLongComment_ThrowsArgumentException()
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync();
        var longComment = new string('A', 1001);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(5, longComment)));

        Assert.Contains("between 5 and 1000 characters", ex.Message);
    }

    [Fact]
    public async Task UpdateOrderReviewAsync_CustomerEditsOwnReview_UpdatesSuccessfully()
    {
        var (_, customerUser, _, _, order) = await SeedOrderAsync();

        await _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(4, "Good quality"));

        var updated = await _reviewService.UpdateOrderReviewAsync(customerUser.Id.ToString(), order.Id, new UpdateOrderReviewRequest(5, "Excellent quality wheat!"));

        Assert.Equal(5, updated.Rating);
        Assert.Equal("Excellent quality wheat!", updated.Comment);
        Assert.NotNull(updated.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateOrderReviewAsync_OtherCustomerEdits_ThrowsInvalidOperationException()
    {
        var (_, custUser, _, _, order) = await SeedOrderAsync();
        var otherUserId = Guid.NewGuid();

        var otherCustUser = new ApplicationUser
        {
            Id = otherUserId,
            UserName = $"other_{otherUserId:N}@test.com",
            Email = $"other_{otherUserId:N}@test.com"
        };
        var otherCustProfile = new CustomerProfile
        {
            UserId = otherUserId,
            FullName = "Other Customer",
            Phone = "9998887776"
        };
        _dbContext.Users.Add(otherCustUser);
        _dbContext.CustomerProfiles.Add(otherCustProfile);
        await _dbContext.SaveChangesAsync();

        await _reviewService.CreateOrderReviewAsync(custUser.Id.ToString(), order.Id, new CreateOrderReviewRequest(4, "Good quality"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reviewService.UpdateOrderReviewAsync(otherUserId.ToString(), order.Id, new UpdateOrderReviewRequest(5, "Hacked")));

        Assert.Contains("own orders", ex.Message);
    }

    [Fact]
    public async Task GetFarmerRatingSummaryAsync_CalculatesAverageAndCountCorrectly()
    {
        var farmerUser = new ApplicationUser
        {
            UserName = $"farmer_{Guid.NewGuid():N}@test.com",
            Email = $"farmer_{Guid.NewGuid():N}@test.com"
        };
        _dbContext.Users.Add(farmerUser);
        await _dbContext.SaveChangesAsync();

        var farmer = new FarmerProfile
        {
            UserId = farmerUser.Id,
            FullName = "Ramesh Farmer",
            Phone = "9876543210"
        };
        _dbContext.FarmerProfiles.Add(farmer);
        await _dbContext.SaveChangesAsync();

        // Seed 3 completed orders for this farmer
        var (_, c1, _, _, o1) = await SeedOrderAsync();
        var (_, c2, _, _, o2) = await SeedOrderAsync();
        var (_, c3, _, _, o3) = await SeedOrderAsync();

        // Reassign farmer to same farmer profile
        o1.FarmerProfileId = farmer.Id;
        o2.FarmerProfileId = farmer.Id;
        o3.FarmerProfileId = farmer.Id;
        await _dbContext.SaveChangesAsync();

        await _reviewService.CreateOrderReviewAsync(c1.Id.ToString(), o1.Id, new CreateOrderReviewRequest(5, "Superb quality"));
        await _reviewService.CreateOrderReviewAsync(c2.Id.ToString(), o2.Id, new CreateOrderReviewRequest(4, "Good quality product"));
        await _reviewService.CreateOrderReviewAsync(c3.Id.ToString(), o3.Id, new CreateOrderReviewRequest(5, "Awesome crop quality"));

        var summary = await _reviewService.GetFarmerRatingSummaryAsync(farmerUser.Id.ToString());

        Assert.Equal(3, summary.TotalReviews);
        // (5 + 4 + 5) / 3 = 4.66666... -> rounded to 1 decimal = 4.7
        Assert.Equal(4.7, summary.AverageRating);
        Assert.Equal(3, summary.RecentReviews.Count);
    }

    [Fact]
    public async Task GetCustomerReviewsAsync_ReturnsAllSubmittedCustomerReviews()
    {
        var (_, customerUser, _, _, o1) = await SeedOrderAsync();

        await _reviewService.CreateOrderReviewAsync(customerUser.Id.ToString(), o1.Id, new CreateOrderReviewRequest(5, "Great product"));

        var history = await _reviewService.GetCustomerReviewsAsync(customerUser.Id.ToString());

        Assert.Single(history);
        Assert.Equal(o1.Id, history[0].OrderId);
        Assert.Equal(5, history[0].Rating);
    }
}
