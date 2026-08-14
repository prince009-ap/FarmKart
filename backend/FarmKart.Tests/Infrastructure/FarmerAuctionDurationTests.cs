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
using FarmKart.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class FarmerAuctionDurationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerAuctionDurationTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_AuctionDurationTest_{Guid.NewGuid()}";
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

    // ──────────────────────────────────────────────
    // Duration Parsing Unit Tests (static method)
    // ──────────────────────────────────────────────

    [Fact]
    public void Test01_ParseDuration_5Hours_Returns_5()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("5 Hours");
        Assert.Equal(5, hours);
    }

    [Fact]
    public void Test02_ParseDuration_12Hours_Returns_12()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("12 Hours");
        Assert.Equal(12, hours);
    }

    [Fact]
    public void Test03_ParseDuration_1Day_Returns_24()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("1 Day");
        Assert.Equal(24, hours);
    }

    [Fact]
    public void Test04_ParseDuration_3Days_Returns_72()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("3 Days");
        Assert.Equal(72, hours);
    }

    [Fact]
    public void Test05_ParseDuration_7Days_Returns_168()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("7 Days");
        Assert.Equal(168, hours);
    }

    [Fact]
    public void Test06_ParseDuration_CustomNumericHours_Returns_CorrectValue()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("8 Hours");
        Assert.Equal(8, hours);
    }

    [Fact]
    public void Test07_ParseDuration_CustomNumericOnly_Returns_CorrectValue()
    {
        var hours = FarmerAuctionService.ParseDurationToHours("36");
        Assert.Equal(36, hours);
    }

    [Fact]
    public void Test08_ParseDuration_Invalid_String_Throws()
    {
        Assert.Throws<ArgumentException>(() => FarmerAuctionService.ParseDurationToHours("forever"));
    }

    [Fact]
    public void Test09_ParseDuration_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => FarmerAuctionService.ParseDurationToHours(""));
    }

    [Fact]
    public void Test10_ParseDuration_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => FarmerAuctionService.ParseDurationToHours("-5"));
    }

    [Fact]
    public void Test11_ParseDuration_CaseInsensitive_Works()
    {
        var h1 = FarmerAuctionService.ParseDurationToHours("5 hours");
        var h2 = FarmerAuctionService.ParseDurationToHours("1 day");
        var h3 = FarmerAuctionService.ParseDurationToHours("7 days");
        Assert.Equal(5, h1);
        Assert.Equal(24, h2);
        Assert.Equal(168, h3);
    }

    // ──────────────────────────────────────────────
    // End-Time Calculation Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void Test12_EndTime_5Hours_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var hours = FarmerAuctionService.ParseDurationToHours("5 Hours");
        var expected = new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, start.AddHours(hours));
    }

    [Fact]
    public void Test13_EndTime_12Hours_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var hours = FarmerAuctionService.ParseDurationToHours("12 Hours");
        var expected = new DateTime(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, start.AddHours(hours));
    }

    [Fact]
    public void Test14_EndTime_1Day_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var hours = FarmerAuctionService.ParseDurationToHours("1 Day");
        var expected = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, start.AddHours(hours));
    }

    [Fact]
    public void Test15_EndTime_3Days_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var hours = FarmerAuctionService.ParseDurationToHours("3 Days");
        var expected = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, start.AddHours(hours));
    }

    [Fact]
    public void Test16_EndTime_7Days_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var hours = FarmerAuctionService.ParseDurationToHours("7 Days");
        var expected = new DateTime(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, start.AddHours(hours));
    }

    // ──────────────────────────────────────────────
    // GetEffectiveStatus Tests
    // ──────────────────────────────────────────────

    [Fact]
    public void Test17_GetEffectiveStatus_BeforeStart_IsScheduled()
    {
        var auction = MakeAuction(DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(7), AuctionStatus.Scheduled);
        var status = FarmerAuctionService.GetEffectiveStatus(auction, DateTime.UtcNow);
        Assert.Equal(AuctionStatus.Scheduled, status);
    }

    [Fact]
    public void Test18_GetEffectiveStatus_DuringAuction_IsLive()
    {
        var auction = MakeAuction(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddHours(5), AuctionStatus.Live);
        var status = FarmerAuctionService.GetEffectiveStatus(auction, DateTime.UtcNow);
        Assert.Equal(AuctionStatus.Live, status);
    }

    [Fact]
    public void Test19_GetEffectiveStatus_AfterEnd_IsEnded()
    {
        var auction = MakeAuction(DateTime.UtcNow.AddHours(-6), DateTime.UtcNow.AddHours(-1), AuctionStatus.Live);
        var status = FarmerAuctionService.GetEffectiveStatus(auction, DateTime.UtcNow);
        Assert.Equal(AuctionStatus.Ended, status);
    }

    [Fact]
    public void Test20_GetEffectiveStatus_Cancelled_IsCancelled()
    {
        var auction = MakeAuction(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(6), AuctionStatus.Cancelled);
        var status = FarmerAuctionService.GetEffectiveStatus(auction, DateTime.UtcNow);
        Assert.Equal(AuctionStatus.Cancelled, status);
    }

    [Fact]
    public void Test21_GetEffectiveStatus_AtExactStart_IsLive()
    {
        var start = DateTime.UtcNow;
        var auction = MakeAuction(start, start.AddHours(5), AuctionStatus.Scheduled);
        var status = FarmerAuctionService.GetEffectiveStatus(auction, start);
        Assert.Equal(AuctionStatus.Live, status);
    }

    [Fact]
    public void Test22_GetEffectiveStatus_AtExactEnd_IsEnded()
    {
        var start = DateTime.UtcNow.AddHours(-5);
        var end = DateTime.UtcNow;
        var auction = MakeAuction(start, end, AuctionStatus.Live);
        var status = FarmerAuctionService.GetEffectiveStatus(auction, end.AddSeconds(1));
        Assert.Equal(AuctionStatus.Ended, status);
    }

    // ──────────────────────────────────────────────
    // Integration Tests: API with Duration
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Test23_Farmer_Can_Create_Auction_With_5Hour_Duration()
    {
        var farmerClient = await GetAuthenticatedFarmerClientAsync("farmer_dur1@test.com", "Password123!", "Duration Farmer 1");
        var cropId = await SeedCropWithStockAsync();

        var startTime = DateTime.UtcNow.AddMinutes(30);
        var request = new CreateFarmerAuctionRequest(
            cropId, 100m, "Kilogram", 50m, 5m,
            startTime, "5 Hours", null
        );

        var res = await farmerClient.PostAsJsonAsync("/api/farmer/auctions", request);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var auction = await res.Content.ReadFromJsonAsync<FarmerAuctionResponse>();
        Assert.NotNull(auction);
        var expectedEnd = startTime.AddHours(5);
        Assert.True(Math.Abs((auction.EndTimeUtc - expectedEnd).TotalSeconds) < 5,
            $"Expected end ~{expectedEnd:u} but got {auction.EndTimeUtc:u}");
    }

    [Fact]
    public async Task Test24_Farmer_Can_Create_Auction_With_1Day_Duration()
    {
        var farmerClient = await GetAuthenticatedFarmerClientAsync("farmer_dur2@test.com", "Password123!", "Duration Farmer 2");
        var cropId = await SeedCropWithStockAsync();

        var startTime = DateTime.UtcNow.AddMinutes(30);
        var request = new CreateFarmerAuctionRequest(
            cropId, 100m, "Kilogram", 50m, 5m,
            startTime, "1 Day", null
        );

        var res = await farmerClient.PostAsJsonAsync("/api/farmer/auctions", request);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var auction = await res.Content.ReadFromJsonAsync<FarmerAuctionResponse>();
        Assert.NotNull(auction);
        var expectedEnd = startTime.AddHours(24);
        Assert.True(Math.Abs((auction.EndTimeUtc - expectedEnd).TotalSeconds) < 5,
            $"Expected end ~{expectedEnd:u} but got {auction.EndTimeUtc:u}");
    }

    [Fact]
    public async Task Test25_Farmer_Create_Auction_Invalid_Duration_Returns_400()
    {
        var farmerClient = await GetAuthenticatedFarmerClientAsync("farmer_dur3@test.com", "Password123!", "Duration Farmer 3");
        var cropId = await SeedCropWithStockAsync();

        var request = new CreateFarmerAuctionRequest(
            cropId, 100m, "Kilogram", 50m, 5m,
            DateTime.UtcNow.AddMinutes(30), "forever", null
        );

        var res = await farmerClient.PostAsJsonAsync("/api/farmer/auctions", request);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test26_FarmerAuctionResponse_Contains_ServerTimeUtc()
    {
        var before = DateTime.UtcNow;
        var farmerClient = await GetAuthenticatedFarmerClientAsync("farmer_dur4@test.com", "Password123!", "Duration Farmer 4");
        var cropId = await SeedCropWithStockAsync();

        var request = new CreateFarmerAuctionRequest(
            cropId, 100m, "Kilogram", 50m, 5m,
            DateTime.UtcNow.AddMinutes(30), "1 Day", null
        );

        var res = await farmerClient.PostAsJsonAsync("/api/farmer/auctions", request);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var auction = await res.Content.ReadFromJsonAsync<FarmerAuctionResponse>();
        Assert.NotNull(auction);
        Assert.True(auction.ServerTimeUtc >= before && auction.ServerTimeUtc <= DateTime.UtcNow.AddSeconds(5),
            $"ServerTimeUtc {auction.ServerTimeUtc:u} out of expected range [{before:u}, now+5s]");
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static Auction MakeAuction(DateTime start, DateTime end, AuctionStatus status)
    {
        var crop = new Crop { CropName = "Test", CropType = "Grain", Area = 1, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 100, Unit = MeasurementUnit.Kilogram };
        var listing = new CropListing { Crop = crop, QuantityForSale = 50, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        return new Auction
        {
            CropListing = listing,
            StartingPrice = 10,
            CurrentHighestBid = 0,
            MinimumBidIncrement = 1,
            StartTimeUtc = start,
            EndTimeUtc = end,
            AuctionStatus = status
        };
    }

    private async Task<HttpClient> GetAuthenticatedFarmerClientAsync(string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Farmer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await authService.RegisterFarmerAsync(new FarmerRegisterRequest(name, email, password, "9999999999", null, "123 Farm Lane", "My Farm", 5m, FarmSizeUnit.Acre, "Surat, Gujarat"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        return client;
    }

    private async Task<Guid> SeedCropWithStockAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var farmer = await db.FarmerProfiles.FirstAsync();
        var crop = new Crop
        {
            FarmerProfileId = farmer.Id,
            CropName = "Duration Test Wheat",
            CropType = "Grain",
            Area = 5,
            AreaUnit = FarmSizeUnit.Acre,
            Status = CropStatus.Harvested,
            Quantity = 1000,
            Unit = MeasurementUnit.Kilogram
        };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();
        db.CropStockTransactions.Add(new CropStockTransaction
        {
            CropId = crop.Id,
            QuantityInBaseUnit = 1000,
            TransactionType = CropStockTransactionType.Harvest,
            Notes = "Test harvest"
        });
        await db.SaveChangesAsync();
        return crop.Id;
    }
}
