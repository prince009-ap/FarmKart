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

public class CustomerBiddingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public CustomerBiddingTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_BiddingTest_{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Secret", "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!" },
                    { "JwtSettings:Issuer", "FarmKart" },
                    { "JwtSettings:Audience", "FarmKartUsers" },
                    { "JwtSettings:ExpiryMinutes", "60" },
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

    [Fact]
    public async Task Test01_Customer_Can_Place_First_Bid_Equal_To_Starting_Price()
    {
        var (customerClient, _) = await GetAuthenticatedCustomerClientAsync("cust_bid1@test.com", "Password123!", "Customer One");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        var res = await customerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var bid = await res.Content.ReadFromJsonAsync<AuctionBidResponse>();
        Assert.NotNull(bid);
        Assert.Equal(25m, bid.Amount);
        Assert.Equal("HIGHEST BID", bid.BidStatus);
    }

    [Fact]
    public async Task Test02_Bid_Below_Starting_Price_Is_Rejected()
    {
        var (customerClient, _) = await GetAuthenticatedCustomerClientAsync("cust_bid2@test.com", "Password123!", "Customer Two");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        var res = await customerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(20m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test03_Subsequent_Bid_Below_Current_Highest_Plus_Increment_Is_Rejected()
    {
        var (customerA, _) = await GetAuthenticatedCustomerClientAsync("cust_bid3a@test.com", "Password123!", "Customer A");
        var (customerB, _) = await GetAuthenticatedCustomerClientAsync("cust_bid3b@test.com", "Password123!", "Customer B");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        await customerA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));

        // Attempt bid of ₹26 (less than 25 + 2 = 27)
        var res = await customerB.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(26m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test04_Valid_Increment_Bid_Is_Accepted_And_Updates_Highest_Bid()
    {
        var (customerA, _) = await GetAuthenticatedCustomerClientAsync("cust_bid4a@test.com", "Password123!", "Customer A");
        var (customerB, _) = await GetAuthenticatedCustomerClientAsync("cust_bid4b@test.com", "Password123!", "Customer B");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        await customerA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));

        var res = await customerB.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(27m));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var bid = await res.Content.ReadFromJsonAsync<AuctionBidResponse>();
        Assert.NotNull(bid);
        Assert.Equal(27m, bid.Amount);

        // Fetch auction details to confirm current highest bid updated to 27
        var auctionRes = await customerA.GetAsync($"/api/customer/auctions/{auctionId}");
        var auction = await auctionRes.Content.ReadFromJsonAsync<CustomerAuctionResponse>();
        Assert.NotNull(auction);
        Assert.Equal(27m, auction.CurrentHighestBid);
    }

    [Fact]
    public async Task Test05_Multiple_Bids_Are_Stored_Separately_In_History()
    {
        var (customerA, _) = await GetAuthenticatedCustomerClientAsync("cust_bid5a@test.com", "Password123!", "Customer A");
        var (customerB, _) = await GetAuthenticatedCustomerClientAsync("cust_bid5b@test.com", "Password123!", "Customer B");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        await customerA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        await customerB.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(27m));
        await customerA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(29m));

        var bidsRes = await customerA.GetAsync($"/api/customer/auctions/{auctionId}/bids");
        Assert.Equal(HttpStatusCode.OK, bidsRes.StatusCode);

        var bids = await bidsRes.Content.ReadFromJsonAsync<List<AuctionBidResponse>>();
        Assert.NotNull(bids);
        Assert.Equal(3, bids.Count);

        Assert.Equal(29m, bids[0].Amount);
        Assert.Equal("HIGHEST BID", bids[0].BidStatus);

        Assert.Equal(27m, bids[1].Amount);
        Assert.Equal("OUTBID", bids[1].BidStatus);

        Assert.Equal(25m, bids[2].Amount);
        Assert.Equal("OUTBID", bids[2].BidStatus);
    }

    [Fact]
    public async Task Test06_Bid_On_Scheduled_Auction_Is_Rejected()
    {
        var (customerClient, _) = await GetAuthenticatedCustomerClientAsync("cust_bid6@test.com", "Password123!", "Customer Six");
        var auctionId = await SeedScheduledAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        var res = await customerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test07_Bid_On_Ended_Auction_Is_Rejected()
    {
        var (customerClient, _) = await GetAuthenticatedCustomerClientAsync("cust_bid7@test.com", "Password123!", "Customer Seven");
        var auctionId = await SeedEndedAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        var res = await customerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test08_Unauthenticated_Request_Is_Rejected_With_401()
    {
        var unauthClient = _factory.CreateClient();
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        var res = await unauthClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Test09_Farmer_Cannot_Place_Customer_Bids_Returns_403()
    {
        var farmerClient = await GetAuthenticatedFarmerClientAsync("farmer_nobid@test.com", "Password123!", "Farmer NoBid");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        var res = await farmerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Test10_GetMyBids_Returns_Authenticated_Customer_Bids_With_Correct_Statuses()
    {
        var (customerA, _) = await GetAuthenticatedCustomerClientAsync("cust_mybids_a@test.com", "Password123!", "Customer MyBids A");
        var (customerB, _) = await GetAuthenticatedCustomerClientAsync("cust_mybids_b@test.com", "Password123!", "Customer MyBids B");
        var auctionId = await SeedLiveAuctionAsync(startingPrice: 25m, minIncrement: 2m);

        // A bids 25 (HIGHEST)
        await customerA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));

        // B bids 27 (B is HIGHEST, A is OUTBID)
        await customerB.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(27m));

        // Query A's bids
        var myBidsRes = await customerA.GetAsync("/api/customer/bids");
        Assert.Equal(HttpStatusCode.OK, myBidsRes.StatusCode);

        var myBids = await myBidsRes.Content.ReadFromJsonAsync<List<CustomerMyBidResponse>>();
        Assert.NotNull(myBids);
        Assert.Single(myBids);
        Assert.Equal(25m, myBids[0].CustomerBidAmount);
        Assert.Equal(27m, myBids[0].CurrentHighestBid);
        Assert.Equal("OUTBID", myBids[0].CustomerBidStatus);

        // Query B's bids
        var myBidsBRes = await customerB.GetAsync("/api/customer/bids");
        var myBidsB = await myBidsBRes.Content.ReadFromJsonAsync<List<CustomerMyBidResponse>>();
        Assert.NotNull(myBidsB);
        Assert.Single(myBidsB);
        Assert.Equal(27m, myBidsB[0].CustomerBidAmount);
        Assert.Equal(27m, myBidsB[0].CurrentHighestBid);
        Assert.Equal("HIGHEST BID", myBidsB[0].CustomerBidStatus);
    }

    // ──────────────────────────────────────────────
    // Helper Seeding Methods
    // ──────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid UserId)> GetAuthenticatedCustomerClientAsync(string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Customer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var reg = await authService.RegisterCustomerAsync(new CustomerRegisterRequest(name, email, password, "9876543210", null, "Surat, Gujarat"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        return (client, reg.UserId);
    }

    private async Task<HttpClient> GetAuthenticatedFarmerClientAsync(string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Farmer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await authService.RegisterFarmerAsync(new FarmerRegisterRequest(name, email, password, "9998887776", null, "123 Farm Way", "Farmer Farm", 10m, FarmSizeUnit.Acre, "Karnal, Haryana"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        return client;
    }

    private async Task<Guid> SeedLiveAuctionAsync(decimal startingPrice, decimal minIncrement)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"live_farmer_{emailSuffix}@test.com", "Password123!", "Live Farmer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Bidding Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = 300, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = 0m,
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddMinutes(-30),
            EndTimeUtc = DateTime.UtcNow.AddHours(5),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedScheduledAuctionAsync(decimal startingPrice, decimal minIncrement)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"sch_farmer_{emailSuffix}@test.com", "Password123!", "Sch Farmer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Scheduled Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = 300, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = 0m,
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddHours(2),
            EndTimeUtc = DateTime.UtcNow.AddHours(7),
            AuctionStatus = AuctionStatus.Scheduled
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedEndedAuctionAsync(decimal startingPrice, decimal minIncrement)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"ended_farmer_{emailSuffix}@test.com", "Password123!", "Ended Farmer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Ended Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = 300, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = 0m,
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }
}
