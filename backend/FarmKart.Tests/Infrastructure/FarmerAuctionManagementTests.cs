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

public class FarmerAuctionManagementTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerAuctionManagementTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_FarmerAuctionMgmtTest_{Guid.NewGuid()}";
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

    private async Task<HttpClient> GetAuthenticatedCustomerClientAsync(string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Customer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await authService.RegisterCustomerAsync(new CustomerRegisterRequest(name, email, password, "9876543210", null, "Surat, Gujarat"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        return client;
    }

    [Fact]
    public async Task Test01_Farmer_Can_Retrieve_Own_Auctions_And_Summary_Counts()
    {
        var email = $"farmer_mgmt_1_{Guid.NewGuid():N}@test.com";
        var client = await GetAuthenticatedFarmerClientAsync(email, "Password123!", "Farmer Ramesh");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var farmer = await db.FarmerProfiles.FirstAsync(f => f.UserId == user.Id);

            var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Basmati Rice", CropType = "Grain", Variety = "Pusa 1121", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 1000, Unit = MeasurementUnit.Kilogram };
            db.Crops.Add(crop);
            await db.SaveChangesAsync();

            var listing1 = new CropListing { CropId = crop.Id, FarmerProfileId = farmer.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
            var listing2 = new CropListing { CropId = crop.Id, FarmerProfileId = farmer.Id, QuantityForSale = 300, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
            db.CropListings.AddRange(listing1, listing2);
            await db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var liveAuction = new Auction { CropListingId = listing1.Id, FarmerProfileId = farmer.Id, StartingPrice = 500, MinimumBidIncrement = 20, StartTimeUtc = now.AddHours(-1), EndTimeUtc = now.AddHours(2), AuctionStatus = AuctionStatus.Live };
            var upcomingAuction = new Auction { CropListingId = listing2.Id, FarmerProfileId = farmer.Id, StartingPrice = 400, MinimumBidIncrement = 15, StartTimeUtc = now.AddHours(2), EndTimeUtc = now.AddHours(5), AuctionStatus = AuctionStatus.Scheduled };
            db.Auctions.AddRange(liveAuction, upcomingAuction);
            await db.SaveChangesAsync();
        }

        var auctionsRes = await client.GetAsync("/api/farmer/auctions");
        Assert.Equal(HttpStatusCode.OK, auctionsRes.StatusCode);
        var auctions = await auctionsRes.Content.ReadFromJsonAsync<List<FarmerAuctionResponse>>();
        Assert.NotNull(auctions);
        Assert.Equal(2, auctions.Count);

        var summaryRes = await client.GetAsync("/api/farmer/auctions/summary");
        Assert.Equal(HttpStatusCode.OK, summaryRes.StatusCode);
        var summary = await summaryRes.Content.ReadFromJsonAsync<FarmerAuctionSummaryCountsResponse>();
        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalAuctions);
        Assert.Equal(1, summary.LiveCount);
        Assert.Equal(1, summary.UpcomingCount);
        Assert.Equal(0, summary.EndedCount);
    }

    [Fact]
    public async Task Test02_Farmer_Cannot_Retrieve_Another_Farmers_Auction()
    {
        var email1 = $"farmer_mgmt_2a_{Guid.NewGuid():N}@test.com";
        var email2 = $"farmer_mgmt_2b_{Guid.NewGuid():N}@test.com";
        var client1 = await GetAuthenticatedFarmerClientAsync(email1, "Password123!", "Farmer One");
        var client2 = await GetAuthenticatedFarmerClientAsync(email2, "Password123!", "Farmer Two");

        Guid farmer1AuctionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user1 = await db.Users.FirstAsync(u => u.Email == email1);
            var farmer1 = await db.FarmerProfiles.FirstAsync(f => f.UserId == user1.Id);

            var crop = new Crop { FarmerProfileId = farmer1.Id, CropName = "Wheat", CropType = "Grain", Quantity = 1000, Unit = MeasurementUnit.Kilogram };
            db.Crops.Add(crop);
            await db.SaveChangesAsync();

            var listing = new CropListing { CropId = crop.Id, FarmerProfileId = farmer1.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction };
            db.CropListings.Add(listing);
            await db.SaveChangesAsync();

            var auction = new Auction { CropListingId = listing.Id, FarmerProfileId = farmer1.Id, StartingPrice = 600, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(5), AuctionStatus = AuctionStatus.Live };
            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            farmer1AuctionId = auction.Id;
        }

        // Farmer 2 attempts to fetch Farmer 1's auction
        var resGet = await client2.GetAsync($"/api/farmer/auctions/{farmer1AuctionId}");
        Assert.Equal(HttpStatusCode.NotFound, resGet.StatusCode);

        var resBids = await client2.GetAsync($"/api/farmer/auctions/{farmer1AuctionId}/bids");
        Assert.Equal(HttpStatusCode.NotFound, resBids.StatusCode);
    }

    [Fact]
    public async Task Test03_Customer_Cannot_Access_Farmer_Auction_Apis()
    {
        var email = $"customer_mgmt_{Guid.NewGuid():N}@test.com";
        var customerClient = await GetAuthenticatedCustomerClientAsync(email, "Password123!", "Customer Priya");

        var res = await customerClient.GetAsync("/api/farmer/auctions");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var resSummary = await customerClient.GetAsync("/api/farmer/auctions/summary");
        Assert.Equal(HttpStatusCode.Forbidden, resSummary.StatusCode);
    }

    [Fact]
    public async Task Test04_GetAuctionBids_Returns_Bids_With_Customer_Public_Name_Only()
    {
        var farmerEmail = $"farmer_mgmt_4_{Guid.NewGuid():N}@test.com";
        var custEmail = $"cust_mgmt_4_{Guid.NewGuid():N}@test.com";

        var farmerClient = await GetAuthenticatedFarmerClientAsync(farmerEmail, "Password123!", "Farmer Ramesh");
        var customerClient = await GetAuthenticatedCustomerClientAsync(custEmail, "Password123!", "Customer Priya");

        Guid auctionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.FirstAsync(u => u.Email == farmerEmail);
            var custUser = await db.Users.FirstAsync(u => u.Email == custEmail);
            var farmer = await db.FarmerProfiles.FirstAsync(f => f.UserId == farmerUser.Id);
            var customer = await db.CustomerProfiles.FirstAsync(c => c.UserId == custUser.Id);

            var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Corn", CropType = "Grain", Quantity = 1000, Unit = MeasurementUnit.Kilogram };
            db.Crops.Add(crop);
            await db.SaveChangesAsync();

            var listing = new CropListing { CropId = crop.Id, FarmerProfileId = farmer.Id, QuantityForSale = 500, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction };
            db.CropListings.Add(listing);
            await db.SaveChangesAsync();

            var auction = new Auction { CropListingId = listing.Id, FarmerProfileId = farmer.Id, StartingPrice = 400, MinimumBidIncrement = 20, StartTimeUtc = DateTime.UtcNow.AddHours(-1), EndTimeUtc = DateTime.UtcNow.AddHours(3), AuctionStatus = AuctionStatus.Live };
            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            var bid1 = new Bid { AuctionId = auction.Id, CustomerProfileId = customer.Id, Amount = 600, RequestedQuantityKg = 250, BidTimeUtc = DateTime.UtcNow.AddMinutes(-30), BidStatus = BidStatus.Active };
            var bid2 = new Bid { AuctionId = auction.Id, CustomerProfileId = customer.Id, Amount = 650, RequestedQuantityKg = 300, BidTimeUtc = DateTime.UtcNow.AddMinutes(-10), BidStatus = BidStatus.Active };
            db.Bids.AddRange(bid1, bid2);
            await db.SaveChangesAsync();

            auctionId = auction.Id;
        }

        var bidsRes = await farmerClient.GetAsync($"/api/farmer/auctions/{auctionId}/bids");
        Assert.Equal(HttpStatusCode.OK, bidsRes.StatusCode);

        var bids = await bidsRes.Content.ReadFromJsonAsync<List<FarmerAuctionBidResponse>>();
        Assert.NotNull(bids);
        Assert.Equal(2, bids.Count);
        Assert.Equal(650, bids[0].BidAmountPerMan);
        Assert.Equal("Customer Priya", bids[0].CustomerName);
        Assert.Equal(300, bids[0].RequestedQuantityKg);
        Assert.Equal(15, bids[0].RequestedQuantityMan);
    }
}
