using FarmKart.Application.DTOs;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public sealed class ReportsAndDisputesTests : IAsyncLifetime
{
    private FarmKartDbContext _dbContext = null!;
    private NotificationService _notificationService = null!;
    private ReportService _reportService = null!;
    private DisputeService _disputeService = null!;

    public async Task InitializeAsync()
    {
        var dbName = $"FarmKartDb_ReportsDisputesTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer($"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        _dbContext = new FarmKartDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _notificationService = new NotificationService(_dbContext);
        _reportService = new ReportService(_dbContext, _notificationService);
        _disputeService = new DisputeService(_dbContext, _notificationService);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task NotificationService_GetPagedNotificationsAsync_SupportsFilteringCategoryAndSearch()
    {
        // Arrange
        var userId = Guid.NewGuid();

        await _notificationService.CreateNotificationAsync(userId.ToString(), "Auction Live", "Your crop auction is live", NotificationType.Auction);
        await _notificationService.CreateNotificationAsync(userId.ToString(), "Order Placed", "Your order #1001 was placed", NotificationType.Order);
        await _notificationService.CreateNotificationAsync(userId.ToString(), "Payment Done", "Payment received successfully", NotificationType.Payment);

        // Act 1: Get all
        var pagedAll = await _notificationService.GetPagedNotificationsAsync(userId, new NotificationQueryRequest(Filter: "all", Page: 1, PageSize: 10));
        Assert.Equal(3, pagedAll.TotalCount);
        Assert.Equal(3, pagedAll.UnreadCount);

        // Act 2: Filter by category Order
        var pagedOrder = await _notificationService.GetPagedNotificationsAsync(userId, new NotificationQueryRequest(Category: "order"));
        Assert.Single(pagedOrder.Items);
        Assert.Equal("Order Placed", pagedOrder.Items[0].Title);

        // Act 3: Search text "Payment"
        var pagedSearch = await _notificationService.GetPagedNotificationsAsync(userId, new NotificationQueryRequest(Search: "Payment"));
        Assert.Single(pagedSearch.Items);
        Assert.Equal("Payment Done", pagedSearch.Items[0].Title);

        // Act 4: Mark as read
        var firstId = pagedAll.Items[0].Id;
        await _notificationService.MarkAsReadAsync(userId, firstId);
        var pagedUnread = await _notificationService.GetPagedNotificationsAsync(userId, new NotificationQueryRequest(Filter: "unread"));
        Assert.Equal(2, pagedUnread.TotalCount);
        Assert.Equal(2, pagedUnread.UnreadCount);

        // Act 5: Mark all as read
        await _notificationService.MarkAllAsReadAsync(userId);
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        Assert.Equal(0, unreadCount.UnreadCount);
    }

    [Fact]
    public async Task ReportService_CreateReportAsync_ValidatesTargetAndPreventsDuplicates()
    {
        // Arrange
        var reporterUserId = Guid.NewGuid();
        var farmerUserId = Guid.NewGuid();

        var farmerUser = new ApplicationUser { Id = farmerUserId, UserName = "farmer_owner", Email = "farmer@test.com" };
        _dbContext.Users.Add(farmerUser);
        await _dbContext.SaveChangesAsync();

        var farmerProfile = new FarmerProfile { UserId = farmerUserId, FullName = "Farmer Listing Owner" };
        _dbContext.FarmerProfiles.Add(farmerProfile);
        await _dbContext.SaveChangesAsync();

        var crop = new Crop { FarmerProfileId = farmerProfile.Id, CropName = "Basmati Rice", CropType = "Rice", Quantity = 500m };
        _dbContext.Crops.Add(crop);
        await _dbContext.SaveChangesAsync();

        var cropListing = new CropListing { FarmerProfileId = farmerProfile.Id, CropId = crop.Id, QuantityForSale = 500m, PricePerUnit = 50m };
        _dbContext.CropListings.Add(cropListing);
        await _dbContext.SaveChangesAsync();

        var auction = new Auction
        {
            FarmerProfileId = farmerProfile.Id,
            CropListingId = cropListing.Id,
            StartingPrice = 500m,
            MinimumBidIncrement = 50m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            AuctionStatus = AuctionStatus.Live
        };
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        // Act 1: Submit valid report for Auction
        var report = await _reportService.CreateReportAsync(reporterUserId, new CreateReportRequest(
            TargetType: ReportTargetType.Auction,
            TargetId: auction.Id,
            Reason: "Suspicious Activity",
            Description: "Starting price seems unrealistically low."
        ));

        Assert.NotNull(report);
        Assert.Equal(ReportStatus.Open, report.Status);
        Assert.Contains("Basmati Rice", report.TargetTitle);

        // Act 2: Attempt duplicate open report
        await Assert.ThrowsAsync<InvalidOperationException>(() => _reportService.CreateReportAsync(reporterUserId, new CreateReportRequest(
            TargetType: ReportTargetType.Auction,
            TargetId: auction.Id,
            Reason: "Suspicious Activity",
            Description: "Duplicate report attempt."
        )));

        // Act 3: Check notification was generated for reporter
        var notifications = await _notificationService.GetNotificationsAsync(reporterUserId);
        Assert.Single(notifications);
        Assert.Equal("Report Submitted", notifications[0].Title);

        // Act 4: User retrieves their own reports
        var userReports = await _reportService.GetUserReportsAsync(reporterUserId, new ReportQueryRequest());
        Assert.Single(userReports.Items);
    }

    [Fact]
    public async Task DisputeService_CreateDisputeAsync_ValidatesEntityParticipation()
    {
        // Arrange
        var customerUserId = Guid.NewGuid();
        var farmerUserId = Guid.NewGuid();
        var strangerUserId = Guid.NewGuid();

        var customerUser = new ApplicationUser { Id = customerUserId, UserName = "customer_a", Email = "customer@test.com" };
        var farmerUser = new ApplicationUser { Id = farmerUserId, UserName = "farmer_b", Email = "farmerb@test.com" };
        _dbContext.Users.AddRange(customerUser, farmerUser);
        await _dbContext.SaveChangesAsync();

        var customerProfile = new CustomerProfile { UserId = customerUserId, FullName = "Customer A" };
        var farmerProfile = new FarmerProfile { UserId = farmerUserId, FullName = "Farmer B" };
        _dbContext.CustomerProfiles.Add(customerProfile);
        _dbContext.FarmerProfiles.Add(farmerProfile);
        await _dbContext.SaveChangesAsync();

        var order = new Order
        {
            OrderNumber = "FK-9901",
            CustomerProfileId = customerProfile.Id,
            TotalAmount = 5000m,
            OrderStatus = OrderStatus.Completed
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act 1: Stranger attempts to raise dispute on order -> Should be blocked (UnauthorizedAccessException)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _disputeService.CreateDisputeAsync(strangerUserId, new CreateDisputeRequest(
            RelatedEntityType: DisputeEntityType.Order,
            RelatedEntityId: order.Id,
            Reason: "Wrong Quantity",
            Description: "I am not involved but raising dispute."
        )));

        // Act 2: Customer (legitimate participant) raises dispute -> Success
        var dispute = await _disputeService.CreateDisputeAsync(customerUserId, new CreateDisputeRequest(
            RelatedEntityType: DisputeEntityType.Order,
            RelatedEntityId: order.Id,
            Reason: "Wrong Quantity",
            Description: "Received 200 Kg instead of 250 Kg ordered."
        ));

        Assert.NotNull(dispute);
        Assert.Equal(DisputeStatus.Open, dispute.Status);
        Assert.Equal(customerUserId.ToString(), dispute.RaisedByUserId);

        // Act 3: Notifications sent to customer (reporter)
        var customerNotes = await _notificationService.GetNotificationsAsync(customerUserId);
        Assert.Single(customerNotes);
        Assert.Equal("Dispute Submitted", customerNotes[0].Title);

        // Act 4: Close dispute
        var closedDispute = await _disputeService.CloseDisputeAsync(customerUserId, dispute.Id, "Issue settled directly");
        Assert.Equal(DisputeStatus.Closed, closedDispute.Status);
        Assert.Equal("Issue settled directly", closedDispute.ResolutionNote);
    }
}
