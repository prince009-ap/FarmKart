using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class MultiQuantityAuctionTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public MultiQuantityAuctionTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_MultiQuantityTest_{Guid.NewGuid()}";
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

        _factory.CreateClient();
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        context.Database.EnsureDeleted();
    }

    private async Task<(HttpClient client, CustomerProfile profile)> GetAuthenticatedCustomerClientAsync(string email, string password, string name)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FarmKart.Infrastructure.Identity.ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

            var user = new FarmKart.Infrastructure.Identity.ApplicationUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = "+919876543210",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
            await userManager.AddToRoleAsync(user, Roles.Customer);

            var profile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = name,
                Phone = "+919876543210"
            };
            db.CustomerProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var profile = await db.CustomerProfiles.FirstAsync(p => p.FullName == name);
            return (client, profile);
        }
    }

    private async Task<Guid> SeedLiveAuctionAsync(decimal auctionQty = 500m, decimal startingPrice = 400m, decimal minIncrement = 20m)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FarmKart.Infrastructure.Identity.ApplicationUser>>();

        var email = $"farmer_mq_{Guid.NewGuid():N}@test.com";
        var user = new FarmKart.Infrastructure.Identity.ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumber = "+919800000000",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user, "Password123!");
        await userManager.AddToRoleAsync(user, Roles.Farmer);

        var farmer = new FarmerProfile
        {
            UserId = user.Id,
            FullName = "Farmer MQ",
            FarmLocation = "Punjab"
        };
        db.FarmerProfiles.Add(farmer);
        await db.SaveChangesAsync();

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id,
            CropName = "Wheat Premium",
            CropType = "Grain",
            Area = 5m,
            AreaUnit = FarmSizeUnit.Acre,
            Status = CropStatus.Harvested,
            Quantity = auctionQty * 2,
            Unit = MeasurementUnit.Kilogram
        };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing
        {
            FarmerProfileId = farmer.Id,
            Crop = crop,
            QuantityForSale = auctionQty,
            Unit = MeasurementUnit.Kilogram,
            ListingType = ListingType.Auction,
            ListingStatus = ListingStatus.Active
        };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = 0m,
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddHours(-1),
            EndTimeUtc = DateTime.UtcNow.AddHours(2),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    [Fact]
    public async Task Test01_BidContainsRequestedQuantityAndValidationWorks()
    {
        var auctionId = await SeedLiveAuctionAsync(500m);
        var (custClient, _) = await GetAuthenticatedCustomerClientAsync($"cust_mq1_{Guid.NewGuid():N}@test.com", "Password123!", "Customer 1");

        // Requesting > 500 Kg (550 Kg) must be rejected
        var overResponse = await custClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(
            Amount: 500m,
            RequestedQuantityKg: 550m
        ));

        Assert.Equal(HttpStatusCode.BadRequest, overResponse.StatusCode);
        var errObj = await overResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(errObj);
        Assert.Contains("exceeds the available auction quantity", errObj["message"]);

        // Requesting 250 Kg must be accepted
        var validResponse = await custClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(
            Amount: 500m,
            RequestedQuantityKg: 250m
        ));

        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);
        var bidRes = await validResponse.Content.ReadFromJsonAsync<AuctionBidResponse>();
        Assert.NotNull(bidRes);
        Assert.Equal(250m, bidRes.RequestedQuantityKg);
        Assert.Equal(12.5m, bidRes.RequestedQuantityMan);
    }

    [Fact]
    public async Task Test02_MultiCustomerBiddingAndOversubscriptionAllocation()
    {
        // Auction: 500 Kg
        var auctionId = await SeedLiveAuctionAsync(500m);
        var (custA, profileA) = await GetAuthenticatedCustomerClientAsync($"cust_mq2a_{Guid.NewGuid():N}@test.com", "Password123!", "Customer A");
        var (custB, profileB) = await GetAuthenticatedCustomerClientAsync($"cust_mq2b_{Guid.NewGuid():N}@test.com", "Password123!", "Customer B");
        var (custC, profileC) = await GetAuthenticatedCustomerClientAsync($"cust_mq2c_{Guid.NewGuid():N}@test.com", "Password123!", "Customer C");

        // Customer A: 300 Kg @ ₹600/Man
        await custA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(600m, 300m));

        // Customer B: 250 Kg @ ₹620/Man
        await custB.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(620m, 250m));

        // Customer C: 100 Kg @ ₹640/Man
        await custC.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(640m, 100m));

        // End auction
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var auc = await db.Auctions.FindAsync(auctionId);
            auc!.EndTimeUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        // Get auction result
        var res = await (await custA.GetAsync($"/api/customer/auctions/{auctionId}/result"))
            .Content.ReadFromJsonAsync<AuctionResultResponse>();

        Assert.NotNull(res);
        Assert.True(res.HasWinner);
        Assert.Equal(500m, res.TotalAuctionQuantityKg);
        Assert.Equal(500m, res.TotalAllocatedQuantityKg);
        Assert.Equal(0m, res.RemainingQuantityKg);
        Assert.Equal(3, res.Allocations.Count);

        // Priority order: C (₹640) -> B (₹620) -> A (₹600)
        // C requested 100 Kg -> Gets 100 Kg (WON), remaining 400 Kg
        // B requested 250 Kg -> Gets 250 Kg (WON), remaining 150 Kg
        // A requested 300 Kg -> Gets 150 Kg (PARTIALLY_WON), remaining 0 Kg
        var allocC = res.Allocations.First(al => al.CustomerProfileId == profileC.Id);
        Assert.Equal(100m, allocC.AllocatedQuantityKg);
        Assert.Equal("WON", allocC.Status);

        var allocB = res.Allocations.First(al => al.CustomerProfileId == profileB.Id);
        Assert.Equal(250m, allocB.AllocatedQuantityKg);
        Assert.Equal("WON", allocB.Status);

        var allocA = res.Allocations.First(al => al.CustomerProfileId == profileA.Id);
        Assert.Equal(150m, allocA.AllocatedQuantityKg);
        Assert.Equal("PARTIALLY_WON", allocA.Status);
    }

    [Fact]
    public async Task Test03_PaymentCalculatesBasedOnAllocatedQuantity()
    {
        var auctionId = await SeedLiveAuctionAsync(500m);
        var (custA, _) = await GetAuthenticatedCustomerClientAsync($"cust_mq3a_{Guid.NewGuid():N}@test.com", "Password123!", "Customer A");

        // Bid 300 Kg @ ₹600 / Man
        await custA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(600m, 300m));

        // End auction
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var auc = await db.Auctions.FindAsync(auctionId);
            auc!.EndTimeUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        // Process Payment: 300 Kg (15 Man) @ ₹600 / Man = ₹9,000
        var payRes = await custA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);
        var payment = await payRes.Content.ReadFromJsonAsync<AuctionPaymentResponse>();

        Assert.NotNull(payment);
        Assert.Equal(300m, payment.AllocatedQuantityKg);
        Assert.Equal(15m, payment.AllocatedQuantityMan);
        Assert.Equal(9000m, payment.TotalPayableAmount);
        Assert.Equal("PAID", payment.PaymentStatus);
    }

    [Fact]
    public async Task Test04_PriceTieUsesEarliestBidFirst()
    {
        var auctionId = await SeedLiveAuctionAsync(300m);
        var (custA, profileA) = await GetAuthenticatedCustomerClientAsync($"cust_mq4a_{Guid.NewGuid():N}@test.com", "Password123!", "Customer A");
        var (custB, profileB) = await GetAuthenticatedCustomerClientAsync($"cust_mq4b_{Guid.NewGuid():N}@test.com", "Password123!", "Customer B");

        // Customer A bids ₹600/Man first
        await custA.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(600m, 200m));
        await Task.Delay(50);
        // Customer B bids ₹620/Man (valid increment) second
        await custB.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(620m, 200m));

        // End auction
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var auc = await db.Auctions.FindAsync(auctionId);
            auc!.EndTimeUtc = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var res = await (await custA.GetAsync($"/api/customer/auctions/{auctionId}/result"))
            .Content.ReadFromJsonAsync<AuctionResultResponse>();

        Assert.NotNull(res);
        Assert.Equal(2, res.Allocations.Count);

        // Customer B (higher rate ₹620/Man) gets 200 Kg (WON). Customer A (₹600/Man) gets remaining 100 Kg (PARTIALLY_WON).
        Assert.Equal(profileB.Id, res.Allocations[0].CustomerProfileId);
        Assert.Equal(200m, res.Allocations[0].AllocatedQuantityKg);
        Assert.Equal("WON", res.Allocations[0].Status);

        Assert.Equal(profileA.Id, res.Allocations[1].CustomerProfileId);
        Assert.Equal(100m, res.Allocations[1].AllocatedQuantityKg);
        Assert.Equal("PARTIALLY_WON", res.Allocations[1].Status);
    }
}
