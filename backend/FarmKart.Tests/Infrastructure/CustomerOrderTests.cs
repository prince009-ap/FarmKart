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

public class CustomerOrderTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public CustomerOrderTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_CustomerOrderTest_{Guid.NewGuid()}";
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
    // Test 1: Customer can retrieve own orders
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test01_Customer_Can_Retrieve_Own_Orders()
    {
        var (c1Client, c1UserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t01@test.com", "Password123!", "Customer T01");
        var (_, c2UserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t01_other@test.com", "Password123!", "Other Customer");

        var orderId = await SeedPaidOrderForCustomerAsync(c1UserId, 300m, 600m);

        var res = await c1Client.GetAsync("/api/customer/orders");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var orders = await res.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(orders);
        Assert.Single(orders);
        Assert.Equal(orderId, orders[0].OrderId);
        Assert.Equal("CONFIRMED", orders[0].Status);
        Assert.Equal(300m, orders[0].AllocatedQuantityKg);
        Assert.Equal(15m, orders[0].AllocatedQuantityMan);
        Assert.Equal(600m, orders[0].PricePerMan);
        Assert.Equal(9000m, orders[0].TotalAmount);
    }

    // -----------------------------------------------------------------------
    // Test 2: Customer A cannot retrieve Customer B's order (returns 404)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test02_Customer_Cannot_Retrieve_Other_Customer_Order()
    {
        var (c1Client, c1UserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t02_a@test.com", "Password123!", "Customer A");
        var (c2Client, c2UserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t02_b@test.com", "Password123!", "Customer B");

        var bOrderId = await SeedPaidOrderForCustomerAsync(c2UserId, 200m, 500m);

        // Customer A attempts to get Customer B's order
        var res = await c1Client.GetAsync($"/api/customer/orders/{bOrderId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 3: Unauthenticated request returns 401 Unauthorized
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test03_Unauthenticated_Request_Returns_401()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/customer/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 4: Non-customer role (e.g. Farmer) cannot access customer order endpoints (returns 403)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test04_Farmer_Role_Cannot_Access_Customer_Orders()
    {
        var (farmerClient, _) = await GetAuthenticatedFarmerClientAsync("farmer_t04@test.com", "Password123!", "Farmer T04");

        var res = await farmerClient.GetAsync("/api/customer/orders");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Test 5: Search filtering by crop name, farmer name, or order number
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test05_Search_Filter_Works()
    {
        var (cClient, cUserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t05@test.com", "Password123!", "Customer T05");

        var wheatOrderId = await SeedPaidOrderForCustomerAsync(cUserId, 200m, 500m, cropName: "Golden Wheat", farmerName: "Ramesh Farmer");
        var riceOrderId = await SeedPaidOrderForCustomerAsync(cUserId, 100m, 800m, cropName: "Basmati Rice", farmerName: "Suresh Farmer");

        // Search by crop name "Wheat"
        var wheatRes = await cClient.GetAsync("/api/customer/orders?search=Wheat");
        Assert.Equal(HttpStatusCode.OK, wheatRes.StatusCode);
        var wheatList = await wheatRes.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(wheatList);
        Assert.Single(wheatList);
        Assert.Equal(wheatOrderId, wheatList[0].OrderId);

        // Search by farmer name "Suresh"
        var farmerRes = await cClient.GetAsync("/api/customer/orders?search=Suresh");
        Assert.Equal(HttpStatusCode.OK, farmerRes.StatusCode);
        var farmerList = await farmerRes.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(farmerList);
        Assert.Single(farmerList);
        Assert.Equal(riceOrderId, farmerList[0].OrderId);
    }

    // -----------------------------------------------------------------------
    // Test 6: Status filtering
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test06_Status_Filter_Works()
    {
        var (cClient, cUserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t06@test.com", "Password123!", "Customer T06");
        var orderId = await SeedPaidOrderForCustomerAsync(cUserId, 200m, 500m);

        // Filter CONFIRMED
        var confirmedRes = await cClient.GetAsync("/api/customer/orders?status=CONFIRMED");
        Assert.Equal(HttpStatusCode.OK, confirmedRes.StatusCode);
        var confirmedList = await confirmedRes.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(confirmedList);
        Assert.Single(confirmedList);

        // Filter DELIVERED (no delivered orders exist)
        var deliveredRes = await cClient.GetAsync("/api/customer/orders?status=DELIVERED");
        Assert.Equal(HttpStatusCode.OK, deliveredRes.StatusCode);
        var deliveredList = await deliveredRes.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(deliveredList);
        Assert.Empty(deliveredList);
    }

    // -----------------------------------------------------------------------
    // Test 7: Sorting by newest and oldest
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test07_Sorting_Newest_And_Oldest()
    {
        var (cClient, cUserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t07@test.com", "Password123!", "Customer T07");

        var order1Id = await SeedPaidOrderForCustomerAsync(cUserId, 200m, 500m, cropName: "Wheat T7");
        await Task.Delay(50); // Ensure distinct timestamps
        var order2Id = await SeedPaidOrderForCustomerAsync(cUserId, 300m, 600m, cropName: "Rice T7");

        // Newest first (default)
        var newestRes = await cClient.GetAsync("/api/customer/orders?sortBy=newest");
        var newestList = await newestRes.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(newestList);
        Assert.Equal(2, newestList.Count);
        Assert.Equal(order2Id, newestList[0].OrderId);
        Assert.Equal(order1Id, newestList[1].OrderId);

        // Oldest first
        var oldestRes = await cClient.GetAsync("/api/customer/orders?sortBy=oldest");
        var oldestList = await oldestRes.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(oldestList);
        Assert.Equal(2, oldestList.Count);
        Assert.Equal(order1Id, oldestList[0].OrderId);
        Assert.Equal(order2Id, oldestList[1].OrderId);
    }

    // -----------------------------------------------------------------------
    // Test 8: Order details includes crop, farmer, and payment breakdown
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test08_Order_Details_Includes_Full_Breakdown()
    {
        var (cClient, cUserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t08@test.com", "Password123!", "Customer T08");
        var orderId = await SeedPaidOrderForCustomerAsync(cUserId, 250m, 600m, cropName: "Organic Wheat", farmerName: "Kisan Farmer");

        var res = await cClient.GetAsync($"/api/customer/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var details = await res.Content.ReadFromJsonAsync<CustomerOrderDetailResponse>();
        Assert.NotNull(details);
        Assert.Equal(orderId, details.OrderId);
        Assert.Equal("Organic Wheat", details.CropName);
        Assert.Equal("Kisan Farmer", details.FarmerName);
        Assert.Equal(250m, details.AllocatedQuantityKg);
        Assert.Equal(12.5m, details.AllocatedQuantityMan);
        Assert.Equal(600m, details.PricePerMan);
        Assert.Equal(7500m, details.TotalAmount);
        Assert.Equal("CONFIRMED", details.Status);
        Assert.Equal("PAID", details.PaymentStatus);
        Assert.StartsWith("FK-TEST-", details.TransactionReference);
    }

    // -----------------------------------------------------------------------
    // Test 9: Partial allocation quantity displayed correctly (not requested qty)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test09_Partial_Allocation_Quantity_Displayed_Correctly()
    {
        var (cClient, cUserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t09@test.com", "Password123!", "Customer T09");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("cust_order_t09_loser@test.com", "Password123!", "Loser T09");

        // Requested 300 Kg, Allocated 200 Kg
        var auctionId = await SeedEndedAuctionWithPartialAllocationAsync(cUserId, loserUserId, 300m, 200m, 600m);

        // Process payment
        var payRes = await cClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);
        var pay = await payRes.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(pay?.Order);

        // Fetch order details via GET /api/customer/orders/{id}
        var res = await cClient.GetAsync($"/api/customer/orders/{pay!.Order!.OrderId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var details = await res.Content.ReadFromJsonAsync<CustomerOrderDetailResponse>();
        Assert.NotNull(details);
        Assert.Equal(200m, details.AllocatedQuantityKg); // NOT 300
        Assert.Equal(10m, details.AllocatedQuantityMan);   // 200/20
        Assert.Equal(6000m, details.TotalAmount);          // 10 * 600
    }

    // -----------------------------------------------------------------------
    // Test 10: Empty order list returned when customer has no orders
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test10_Empty_Order_List_Returned()
    {
        var (cClient, _) = await GetAuthenticatedCustomerClientAsync("cust_order_t10_empty@test.com", "Password123!", "Customer Empty");

        var res = await cClient.GetAsync("/api/customer/orders");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<CustomerOrderListItemResponse>>();
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    // -----------------------------------------------------------------------
    // Test 11: Invalid order GUID returns 404
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Test11_Invalid_Order_Guid_Returns_404()
    {
        var (cClient, _) = await GetAuthenticatedCustomerClientAsync("cust_order_t11@test.com", "Password123!", "Customer T11");

        var randomId = Guid.NewGuid();
        var res = await cClient.GetAsync($"/api/customer/orders/{randomId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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

    private async Task<Guid> SeedPaidOrderForCustomerAsync(
        Guid customerUserId, decimal quantityKg, decimal pricePerMan, string cropName = "Wheat", string farmerName = "Farmer")
    {
        var emailSuffix = Guid.NewGuid().ToString("N")[..8];
        var (_, dummyLoserId) = await GetAuthenticatedCustomerClientAsync($"loser_{emailSuffix}@test.com", "Password123!", "Loser");

        var auctionId = await SeedEndedAuctionWithAllocationAsync(customerUserId, dummyLoserId, quantityKg, pricePerMan, AllocationStatus.Won, cropName, farmerName);

        // Process payment as customer
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var customer = await db.CustomerProfiles.FirstAsync(c => c.UserId == customerUserId);

        var (client, _) = await GetAuthenticatedCustomerClientAsync($"pay_runner_{emailSuffix}@test.com", "Password123!", "Runner");
        // We will call the payment API using c1Client
        var c1Client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // Instead of calling client, let's process payment directly via API call
        var authUser = await db.Users.FirstAsync(u => u.Id == customerUserId);
        var loginRes = await c1Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(authUser.Email!, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var res = await c1Client.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var paymentResponse = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(paymentResponse?.Order);

        return paymentResponse!.Order!.OrderId;
    }

    private async Task<Guid> SeedEndedAuctionWithAllocationAsync(
        Guid winnerUserId, Guid loserUserId, decimal quantityKg, decimal pricePerMan, AllocationStatus allocationStatus, string cropName = "Test Crop", string farmerName = "Test Farmer")
    {
        var emailSuffix = Guid.NewGuid().ToString("N")[..8];
        await GetAuthenticatedFarmerClientAsync($"farmer_{emailSuffix}@test.com", "Password123!", farmerName);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var winner = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);
        var loser = await db.CustomerProfiles.FirstAsync(c => c.UserId == loserUserId);

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id, CropName = cropName, CropType = "Grain",
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
}
