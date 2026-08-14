using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.Auctions;
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

public class AuctionFinalizationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public AuctionFinalizationTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_FinalizationTest_{Guid.NewGuid()}";
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

    [Fact]
    public async Task Test01_Expired_Auction_With_Bids_Finalizes_Highest_Bidder_As_Winner()
    {
        using var scope = _factory.Services.CreateScope();
        var finalizationService = scope.ServiceProvider.GetRequiredService<IAuctionFinalizationService>();

        var (auctionId, customerAId, customerBId) = await SeedExpiredAuctionWithBidsAsync(25m, 2m, 29m, 31m);

        var finalizedCount = await finalizationService.FinalizeExpiredAuctionsAsync();
        Assert.True(finalizedCount >= 1);

        var result = await finalizationService.GetAuctionResultAsync(auctionId);
        Assert.NotNull(result);
        Assert.True(result.HasWinner);
        Assert.Equal(31m, result.WinningBidAmount);
        Assert.Equal(customerBId, result.WinnerCustomerProfileId);
    }

    [Fact]
    public async Task Test02_Expired_Auction_With_Zero_Bids_Ends_Without_Winner()
    {
        using var scope = _factory.Services.CreateScope();
        var finalizationService = scope.ServiceProvider.GetRequiredService<IAuctionFinalizationService>();

        var auctionId = await SeedExpiredAuctionWithZeroBidsAsync(25m, 2m);

        await finalizationService.FinalizeExpiredAuctionsAsync();

        var result = await finalizationService.GetAuctionResultAsync(auctionId);
        Assert.NotNull(result);
        Assert.False(result.HasWinner);
        Assert.Null(result.WinningBidAmount);
        Assert.Null(result.WinnerCustomerName);
        Assert.Equal("ENDED", result.AuctionStatus);
    }

    [Fact]
    public async Task Test03_Finalization_Is_Idempotent_And_Does_Not_Duplicate_Winners()
    {
        using var scope = _factory.Services.CreateScope();
        var finalizationService = scope.ServiceProvider.GetRequiredService<IAuctionFinalizationService>();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var (auctionId, _, customerBId) = await SeedExpiredAuctionWithBidsAsync(25m, 2m, 29m, 31m);

        // Run finalization 3 times
        await finalizationService.FinalizeExpiredAuctionsAsync();
        await finalizationService.FinalizeExpiredAuctionsAsync();
        await finalizationService.FinalizeExpiredAuctionsAsync();

        var winnerRecords = await db.AuctionWinners.Where(w => w.AuctionId == auctionId).ToListAsync();
        Assert.Single(winnerRecords);
        Assert.Equal(customerBId, winnerRecords[0].CustomerProfileId);
        Assert.Equal(31m, winnerRecords[0].FinalAmount);
    }

    [Fact]
    public async Task Test04_Post_End_Bids_Are_Strictly_Rejected()
    {
        var (customerClient, _) = await GetAuthenticatedCustomerClientAsync("cust_postend@test.com", "Password123!", "Customer PostEnd");
        var auctionId = await SeedExpiredAuctionWithZeroBidsAsync(25m, 2m);

        var res = await customerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/bids", new PlaceBidRequest(25m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test05_Customer_Result_Returns_WON_For_Winner_And_LOST_For_Loser()
    {
        var (customerAClient, customerAUserId) = await GetAuthenticatedCustomerClientAsync("cust_res_a@test.com", "Password123!", "Customer Winner");
        var (customerBClient, customerBUserId) = await GetAuthenticatedCustomerClientAsync("cust_res_b@test.com", "Password123!", "Customer Loser");

        var auctionId = await SeedExpiredAuctionWithBidsForUsersAsync(customerAUserId, customerBUserId, 25m, 2m, 31m, 27m);

        using (var scope = _factory.Services.CreateScope())
        {
            var finalizationService = scope.ServiceProvider.GetRequiredService<IAuctionFinalizationService>();
            await finalizationService.FinalizeExpiredAuctionsAsync();
        }

        // Query winner result
        var resWinner = await customerAClient.GetAsync($"/api/customer/auctions/{auctionId}/result");
        Assert.Equal(HttpStatusCode.OK, resWinner.StatusCode);
        var resultWinner = await resWinner.Content.ReadFromJsonAsync<AuctionResultResponse>();
        Assert.NotNull(resultWinner);
        Assert.Equal("WON", resultWinner.CustomerResultStatus);

        // Query loser result
        var resLoser = await customerBClient.GetAsync($"/api/customer/auctions/{auctionId}/result");
        Assert.Equal(HttpStatusCode.OK, resLoser.StatusCode);
        var resultLoser = await resLoser.Content.ReadFromJsonAsync<AuctionResultResponse>();
        Assert.NotNull(resultLoser);
        Assert.Equal("LOST", resultLoser.CustomerResultStatus);
    }

    [Fact]
    public async Task Test06_Farmer_Can_Retrieve_Auction_Result_With_Winner_Info()
    {
        var (farmerClient, farmerUserId) = await GetAuthenticatedFarmerClientAsync("farmer_res@test.com", "Password123!", "Farmer Result");
        var (customerClient, customerUserId) = await GetAuthenticatedCustomerClientAsync("cust_res_farmer@test.com", "Password123!", "Winning Customer");

        var auctionId = await SeedExpiredAuctionForFarmerAsync(farmerUserId, customerUserId, 25m, 2m, 33m);

        using (var scope = _factory.Services.CreateScope())
        {
            var finalizationService = scope.ServiceProvider.GetRequiredService<IAuctionFinalizationService>();
            await finalizationService.FinalizeExpiredAuctionsAsync();
        }

        var res = await farmerClient.GetAsync($"/api/farmer/auctions/{auctionId}/result");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var result = await res.Content.ReadFromJsonAsync<AuctionResultResponse>();
        Assert.NotNull(result);
        Assert.True(result.HasWinner);
        Assert.Equal(33m, result.WinningBidAmount);
        Assert.Equal("Winning Customer", result.WinnerCustomerName);
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

    private async Task<(HttpClient Client, Guid UserId)> GetAuthenticatedFarmerClientAsync(string email, string password, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(Roles.Farmer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var reg = await authService.RegisterFarmerAsync(new FarmerRegisterRequest(name, email, password, "9998887776", null, "123 Farm Way", "Farmer Farm", 10m, FarmSizeUnit.Acre, "Karnal, Haryana"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        return (client, reg.UserId);
    }

    private async Task<(Guid AuctionId, Guid CustomerAProfileId, Guid CustomerBProfileId)> SeedExpiredAuctionWithBidsAsync(
        decimal startingPrice, decimal minIncrement, decimal bidAAmount, decimal bidBAmount)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"exp_farmer_{emailSuffix}@test.com", "Password123!", "Exp Farmer");
        var (_, custAUserId) = await GetAuthenticatedCustomerClientAsync($"cust_a_{emailSuffix}@test.com", "Password123!", "Customer A");
        var (_, custBUserId) = await GetAuthenticatedCustomerClientAsync($"cust_b_{emailSuffix}@test.com", "Password123!", "Customer B");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var customerA = await db.CustomerProfiles.FirstAsync(c => c.UserId == custAUserId);
        var customerB = await db.CustomerProfiles.FirstAsync(c => c.UserId == custBUserId);

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Final Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
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
            CurrentHighestBid = Math.Max(bidAAmount, bidBAmount),
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bidA = new Bid { AuctionId = auction.Id, CustomerProfileId = customerA.Id, Amount = bidAAmount, BidTimeUtc = DateTime.UtcNow.AddHours(-4), BidStatus = BidStatus.Active };
        var bidB = new Bid { AuctionId = auction.Id, CustomerProfileId = customerB.Id, Amount = bidBAmount, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.AddRange(bidA, bidB);
        await db.SaveChangesAsync();

        return (auction.Id, customerA.Id, customerB.Id);
    }

    private async Task<Guid> SeedExpiredAuctionWithZeroBidsAsync(decimal startingPrice, decimal minIncrement)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"zero_farmer_{emailSuffix}@test.com", "Password123!", "Zero Farmer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Zero Bids Rice", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
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
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedExpiredAuctionWithBidsForUsersAsync(
        Guid userAId, Guid userBId, decimal startingPrice, decimal minIncrement, decimal bidAAmount, decimal bidBAmount)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"user_farmer_{emailSuffix}@test.com", "Password123!", "User Farmer");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var custA = await db.CustomerProfiles.FirstAsync(c => c.UserId == userAId);
        var custB = await db.CustomerProfiles.FirstAsync(c => c.UserId == userBId);

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "User Bids Corn", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
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
            CurrentHighestBid = Math.Max(bidAAmount, bidBAmount),
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bidA = new Bid { AuctionId = auction.Id, CustomerProfileId = custA.Id, Amount = bidAAmount, BidTimeUtc = DateTime.UtcNow.AddHours(-4), BidStatus = BidStatus.Active };
        var bidB = new Bid { AuctionId = auction.Id, CustomerProfileId = custB.Id, Amount = bidBAmount, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.AddRange(bidA, bidB);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedExpiredAuctionForFarmerAsync(
        Guid farmerUserId, Guid customerUserId, decimal startingPrice, decimal minIncrement, decimal winningAmount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.FirstAsync(f => f.UserId == farmerUserId);
        var customer = await db.CustomerProfiles.FirstAsync(c => c.UserId == customerUserId);

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Farmer Result Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 500, Unit = MeasurementUnit.Kilogram };
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
            CurrentHighestBid = winningAmount,
            MinimumBidIncrement = minIncrement,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bid = new Bid { AuctionId = auction.Id, CustomerProfileId = customer.Id, Amount = winningAmount, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.Add(bid);
        await db.SaveChangesAsync();

        return auction.Id;
    }
}
