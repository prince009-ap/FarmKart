using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class OrderCreationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public OrderCreationTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_OrderTest_{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Secret", "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!" },
                    { "JwtSettings:Issuer", "FarmKart" },
                    { "JwtSettings:Audience", "FarmKartUsers" },
                    { "JwtSettings:CookieName", "FarmKartAuth" },
                    { "JwtSettings:CookieSecure", "false" },
                    { "JwtSettings:CookieSameSite", "Lax" }
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FarmKartDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<FarmKartDbContext>(options =>
                    options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True"));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                context.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        context.Database.EnsureDeleted();
    }

    // -----------------------------------------------------------------------
    // Test 1: PAID winning payment creates order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test01_PaidWinningPayment_CreatesOrder()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t01_winner@test.com", "Password123!", "Winner T01");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t01_loser@test.com", "Password123!", "Loser T01");

        // 300 Kg @ ₹600/Man → total = 300/20 * 600 = 9000
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 300m, 600m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal("PAID", payment.PaymentStatus);
        Assert.NotNull(payment.Order);
        Assert.StartsWith("FK-", payment.Order!.OrderNumber);
        Assert.Equal("CONFIRMED", payment.Order.Status);
        Assert.Equal(300m, payment.Order.AllocatedQuantityKg);
        Assert.Equal(15m, payment.Order.AllocatedQuantityMan); // 300/20
        Assert.Equal(600m, payment.Order.PricePerMan);
        Assert.Equal(9000m, payment.Order.TotalAmount);
    }

    // -----------------------------------------------------------------------
    // Test 2: PENDING payment does NOT create order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test02_PendingPayment_DoesNotCreateOrder()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t02_winner@test.com", "Password123!", "Winner T02");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t02_loser@test.com", "Password123!", "Loser T02");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 500m, AllocationStatus.Won);

        // Manually insert a PENDING payment and verify no order
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var winner = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);
        var pendingPmt = new AuctionPayment
        {
            AuctionId = auctionId,
            CustomerProfileId = winner.Id,
            Amount = 5000m,
            AllocatedQuantityKg = 200m,
            Currency = "INR",
            PaymentMethod = PaymentMethod.Upi,
            PaymentStatus = PaymentStatus.Pending,
            TransactionReference = "PENDING-001"
        };
        db.AuctionPayments.Add(pendingPmt);
        await db.SaveChangesAsync();

        var orderCount = await db.AuctionOrders.CountAsync(o => o.AuctionPaymentId == pendingPmt.Id);
        Assert.Equal(0, orderCount);
    }

    // -----------------------------------------------------------------------
    // Test 3: Idempotency — same payment does not create duplicate order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test03_DuplicatePaymentProcessing_DoesNotCreateDuplicateOrder()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t03_winner@test.com", "Password123!", "Winner T03");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t03_loser@test.com", "Password123!", "Loser T03");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 400m, 700m, AllocationStatus.Won);

        // First payment
        var res1 = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var pay1 = await res1.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(pay1?.Order);
        var orderNumber1 = pay1!.Order!.OrderNumber;

        // Second call to same auction payment (idempotent — already PAID)
        var res2 = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var pay2 = await res2.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(pay2?.Order);
        Assert.Equal(orderNumber1, pay2!.Order!.OrderNumber); // same order

        // Verify only one AuctionOrder in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var ordersForThisPayment = await db.AuctionOrders
            .Where(o => o.AuctionPaymentId == Guid.Parse(pay1.PaymentId.ToString()))
            .CountAsync();
        Assert.Equal(1, ordersForThisPayment);
    }

    // -----------------------------------------------------------------------
    // Test 4: Order quantity = AllocatedQuantityKg, NOT RequestedQuantityKg
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test04_OrderQuantity_EqualsAllocatedQuantityKg_NotRequested()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t04_winner@test.com", "Password123!", "Winner T04");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t04_loser@test.com", "Password123!", "Loser T04");

        // Partial win: requested 300, allocated 250
        var auctionId = await SeedEndedAuctionWithPartialAllocationAsync(winnerUserId, loserUserId, 300m, 250m, 600m);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);
        Assert.Equal(250m, payment!.Order!.AllocatedQuantityKg); // NOT 300
        Assert.Equal(12.5m, payment.Order.AllocatedQuantityMan); // 250/20
        Assert.Equal(7500m, payment.Order.TotalAmount);          // 12.5 * 600
    }

    // -----------------------------------------------------------------------
    // Test 5: Price stored as ₹/Man, total = Kg/20 * PricePerMan
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test05_PriceAndTotal_CalculatedCorrectly()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t05_winner@test.com", "Password123!", "Winner T05");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t05_loser@test.com", "Password123!", "Loser T05");

        // 250 Kg @ ₹600/Man = 12.5 Man * 600 = ₹7,500
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 250m, 600m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);
        Assert.Equal(600m, payment!.Order!.PricePerMan);
        Assert.Equal(7500m, payment.Order.TotalAmount);
    }

    // -----------------------------------------------------------------------
    // Test 6: WON allocation creates order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test06_WonAllocation_CreatesOrder()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t06_winner@test.com", "Password123!", "Winner T06");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t06_loser@test.com", "Password123!", "Loser T06");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 550m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);
        Assert.Equal("CONFIRMED", payment!.Order!.Status);
    }

    // -----------------------------------------------------------------------
    // Test 7: PARTIALLY_WON allocation creates order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test07_PartiallyWonAllocation_CreatesOrder()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t07_winner@test.com", "Password123!", "Winner T07");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t07_loser@test.com", "Password123!", "Loser T07");

        var auctionId = await SeedEndedAuctionWithPartialAllocationAsync(winnerUserId, loserUserId, 300m, 200m, 640m);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);
        Assert.Equal(200m, payment!.Order!.AllocatedQuantityKg);
        Assert.Equal("CONFIRMED", payment.Order.Status);
    }

    // -----------------------------------------------------------------------
    // Test 8: OrderNumber is unique per order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test08_OrderNumber_IsUniqueAcrossOrders()
    {
        var (w1Client, w1UserId) = await GetAuthenticatedCustomerClientAsync("order_t08_w1@test.com", "Password123!", "Winner T08A");
        var (w2Client, w2UserId) = await GetAuthenticatedCustomerClientAsync("order_t08_w2@test.com", "Password123!", "Winner T08B");

        // Two separate auctions — two orders
        var emailSuffix = Guid.NewGuid().ToString("N")[..8];
        await GetAuthenticatedFarmerClientAsync($"farmer_t08_{emailSuffix}@test.com", "Password123!", "Farmer T08");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var w1 = await db.CustomerProfiles.FirstAsync(c => c.UserId == w1UserId);
        var w2 = await db.CustomerProfiles.FirstAsync(c => c.UserId == w2UserId);

        var auction1Id = await CreateMinimalAuctionWithAllocation(db, farmer, w1, 200m, 500m, AllocationStatus.Won);
        var auction2Id = await CreateMinimalAuctionWithAllocation(db, farmer, w2, 200m, 500m, AllocationStatus.Won);

        var res1 = await w1Client.PostAsJsonAsync($"/api/customer/auctions/{auction1Id}/payments", new ProcessPaymentRequest("UPI"));
        var res2 = await w2Client.PostAsJsonAsync($"/api/customer/auctions/{auction2Id}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);

        var pay1 = await res1.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        var pay2 = await res2.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(pay1?.Order);
        Assert.NotNull(pay2?.Order);
        Assert.NotEqual(pay1!.Order!.OrderNumber, pay2!.Order!.OrderNumber);
    }

    // -----------------------------------------------------------------------
    // Test 9: Initial order status is CONFIRMED
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test09_InitialOrderStatus_IsConfirmed()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t09_winner@test.com", "Password123!", "Winner T09");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t09_loser@test.com", "Password123!", "Loser T09");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 450m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();

        Assert.NotNull(payment?.Order);
        Assert.Equal("CONFIRMED", payment!.Order!.Status);
    }

    // -----------------------------------------------------------------------
    // Test 10: Order references correct FarmerId (derived from auction)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test10_OrderFarmerProfile_MatchesAuctionFarmer()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t10_winner@test.com", "Password123!", "Winner T10");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t10_loser@test.com", "Password123!", "Loser T10");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 300m, 600m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var order = await db.AuctionOrders.FirstAsync(o => o.Id == payment!.Order!.OrderId);
        var auction = await db.Auctions.FirstAsync(a => a.Id == auctionId);
        Assert.Equal(auction.FarmerProfileId, order.FarmerProfileId);
    }

    // -----------------------------------------------------------------------
    // Test 11: Order references correct CustomerProfile
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test11_OrderCustomerProfile_MatchesWinningCustomer()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t11_winner@test.com", "Password123!", "Winner T11");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t11_loser@test.com", "Password123!", "Loser T11");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 480m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var order = await db.AuctionOrders.FirstAsync(o => o.Id == payment!.Order!.OrderId);
        var customerProfile = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);
        Assert.Equal(customerProfile.Id, order.CustomerProfileId);
    }

    // -----------------------------------------------------------------------
    // Test 12: Losing customer (no allocation) cannot process payment → no order
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test12_LostAllocation_CannotCreateOrderViaPayment()
    {
        var (_, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t12_winner@test.com", "Password123!", "Winner T12");
        var (loserClient, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t12_loser@test.com", "Password123!", "Loser T12");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 500m, AllocationStatus.Won);

        var res = await loserClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 13: Order AuctionPaymentId references correct payment
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test13_Order_ReferencesCorrectPayment()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t13_winner@test.com", "Password123!", "Winner T13");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t13_loser@test.com", "Password123!", "Loser T13");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 520m, AllocationStatus.Won);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);
        Assert.Equal(payment!.PaymentId, payment.Order!.AuctionPaymentId);
    }

    // -----------------------------------------------------------------------
    // Test 14: Live auction payment returns 400 (no order)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test14_LiveAuction_PaymentFails_NoOrder()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t14_winner@test.com", "Password123!", "Winner T14");
        var auctionId = await SeedLiveAuctionAsync(winnerUserId);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var orderCount = await db.AuctionOrders.CountAsync(o => o.AuctionId == auctionId);
        Assert.Equal(0, orderCount);
    }

    // -----------------------------------------------------------------------
    // Test 15: GetPaymentById returns order if PAID
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test15_GetPaymentById_IncludesOrderWhenPaid()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("order_t15_winner@test.com", "Password123!", "Winner T15");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("order_t15_loser@test.com", "Password123!", "Loser T15");
        var auctionId = await SeedEndedAuctionWithAllocationAsync(winnerUserId, loserUserId, 200m, 500m, AllocationStatus.Won);

        var payRes = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        var payment = await payRes.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment?.Order);

        var getRes = await winnerClient.GetAsync($"/api/customer/payments/{payment!.PaymentId}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetchedPayment = await getRes.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(fetchedPayment?.Order);
        Assert.Equal(payment.Order!.OrderNumber, fetchedPayment!.Order!.OrderNumber);
    }

    // ============================================================
    // HELPER METHODS
    // ============================================================

    private async Task<(HttpClient Client, Guid UserId)> GetAuthenticatedCustomerClientAsync(
        string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Customer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var reg = await authService.RegisterCustomerAsync(new CustomerRegisterRequest(name, email, password, "9876543210", "Surat", "Gujarat"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        return (client, reg.UserId);
    }

    private async Task<(HttpClient Client, Guid UserId)> GetAuthenticatedFarmerClientAsync(
        string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Farmer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var reg = await authService.RegisterFarmerAsync(new FarmerRegisterRequest(
            name, email, password, "9998887776", null, "123 Farm Way", "Test Farm", 10m, FarmSizeUnit.Acre, "Karnal, Haryana"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        return (client, reg.UserId);
    }

    private async Task<Guid> SeedEndedAuctionWithAllocationAsync(
        Guid winnerUserId, Guid loserUserId, decimal quantityKg, decimal pricePerMan, AllocationStatus allocationStatus)
    {
        var emailSuffix = Guid.NewGuid().ToString("N")[..8];
        await GetAuthenticatedFarmerClientAsync($"farmer_{emailSuffix}@test.com", "Password123!", "Farmer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var winner = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);
        var loser = await db.CustomerProfiles.FirstAsync(c => c.UserId == loserUserId);

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id, CropName = "Test Wheat", CropType = "Grain",
            Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested,
            Quantity = 1000, Unit = MeasurementUnit.Kilogram
        };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing
        {
            FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = quantityKg,
            Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active
        };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id, FarmerProfileId = farmer.Id,
            StartingPrice = 25m, CurrentHighestBid = pricePerMan, MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bidLoser = new Bid { AuctionId = auction.Id, CustomerProfileId = loser.Id, Amount = 25m, BidTimeUtc = DateTime.UtcNow.AddHours(-4), BidStatus = BidStatus.Active };
        var bidWinner = new Bid { AuctionId = auction.Id, CustomerProfileId = winner.Id, Amount = pricePerMan, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.AddRange(bidLoser, bidWinner);
        await db.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id, BidId = bidWinner.Id, CustomerProfileId = winner.Id,
            RequestedQuantityKg = quantityKg, AllocatedQuantityKg = quantityKg,
            WinningBidAmountPerMan = pricePerMan, Status = allocationStatus,
            FinalizedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.AuctionAllocations.Add(allocation);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedEndedAuctionWithPartialAllocationAsync(
        Guid winnerUserId, Guid loserUserId, decimal requestedKg, decimal allocatedKg, decimal pricePerMan)
    {
        var emailSuffix = Guid.NewGuid().ToString("N")[..8];
        await GetAuthenticatedFarmerClientAsync($"farmer_partial_{emailSuffix}@test.com", "Password123!", "Farmer Partial");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var winner = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);
        var loser = await db.CustomerProfiles.FirstAsync(c => c.UserId == loserUserId);

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id, CropName = "Partial Wheat", CropType = "Grain",
            Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested,
            Quantity = 1000, Unit = MeasurementUnit.Kilogram
        };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        // Listing quantity = allocatedKg (what's actually available)
        var listing = new CropListing
        {
            FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = allocatedKg,
            Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active
        };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id, FarmerProfileId = farmer.Id,
            StartingPrice = 25m, CurrentHighestBid = pricePerMan, MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bidLoser = new Bid { AuctionId = auction.Id, CustomerProfileId = loser.Id, Amount = 24m, BidTimeUtc = DateTime.UtcNow.AddHours(-4), BidStatus = BidStatus.Active };
        var bidWinner = new Bid { AuctionId = auction.Id, CustomerProfileId = winner.Id, Amount = pricePerMan, RequestedQuantityKg = requestedKg, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.AddRange(bidLoser, bidWinner);
        await db.SaveChangesAsync();

        // Partial allocation: allocated < requested
        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id, BidId = bidWinner.Id, CustomerProfileId = winner.Id,
            RequestedQuantityKg = requestedKg, AllocatedQuantityKg = allocatedKg,
            WinningBidAmountPerMan = pricePerMan, Status = AllocationStatus.PartiallyWon,
            FinalizedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.AuctionAllocations.Add(allocation);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedLiveAuctionAsync(Guid customerUserId)
    {
        var emailSuffix = Guid.NewGuid().ToString("N")[..8];
        await GetAuthenticatedFarmerClientAsync($"farmer_live_{emailSuffix}@test.com", "Password123!", "Farmer Live");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Live Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 1000, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = 200m, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id, FarmerProfileId = farmer.Id,
            StartingPrice = 400m, CurrentHighestBid = 0m, MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddMinutes(-30),
            EndTimeUtc = DateTime.UtcNow.AddHours(5),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> CreateMinimalAuctionWithAllocation(
        FarmKartDbContext db, FarmerProfile farmer, CustomerProfile winner,
        decimal quantityKg, decimal pricePerMan, AllocationStatus status)
    {
        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = $"Crop-{Guid.NewGuid():N}", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 1000, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = quantityKg, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id, FarmerProfileId = farmer.Id,
            StartingPrice = 25m, CurrentHighestBid = pricePerMan, MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6), EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bid = new Bid { AuctionId = auction.Id, CustomerProfileId = winner.Id, Amount = pricePerMan, RequestedQuantityKg = quantityKg, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.Add(bid);
        await db.SaveChangesAsync();

        var allocation = new AuctionAllocation
        {
            AuctionId = auction.Id, BidId = bid.Id, CustomerProfileId = winner.Id,
            RequestedQuantityKg = quantityKg, AllocatedQuantityKg = quantityKg,
            WinningBidAmountPerMan = pricePerMan, Status = status,
            FinalizedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.AuctionAllocations.Add(allocation);
        await db.SaveChangesAsync();

        return auction.Id;
    }
}
