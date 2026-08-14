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

public class CustomerPaymentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public CustomerPaymentTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_PaymentTest_{Guid.NewGuid()}";
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
    public async Task Test01_Winning_Customer_Can_Process_Payment_For_Ended_Auction()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("winner_pay1@test.com", "Password123!", "Winning Customer");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("loser_pay1@test.com", "Password123!", "Losing Customer");

        // 300 Kg (15 Man) @ ₹600/Man = ₹9,000
        var auctionId = await SeedEndedAuctionWithWinnerAsync(winnerUserId, loserUserId, 300m, 600m);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var payment = await res.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(payment);
        Assert.Equal(9000m, payment.TotalPayableAmount);
        Assert.Equal("PAID", payment.PaymentStatus);
        Assert.StartsWith("FK-TEST-", payment.TransactionReference);
    }

    [Fact]
    public async Task Test02_Losing_Customer_Cannot_Process_Payment_Returns_403_Forbidden()
    {
        var (_, winnerUserId) = await GetAuthenticatedCustomerClientAsync("winner_pay2@test.com", "Password123!", "Winner 2");
        var (loserClient, loserUserId) = await GetAuthenticatedCustomerClientAsync("loser_pay2@test.com", "Password123!", "Loser 2");

        var auctionId = await SeedEndedAuctionWithWinnerAsync(winnerUserId, loserUserId, 300m, 600m);

        var res = await loserClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Test03_Payment_On_Live_Auction_Is_Rejected()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("winner_pay3@test.com", "Password123!", "Winner 3");
        var auctionId = await SeedLiveAuctionAsync(winnerUserId, 300m, 500m);

        var res = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Test04_Payment_Is_Idempotent_And_Does_Not_Duplicate_Paid_Record()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("winner_pay4@test.com", "Password123!", "Winner 4");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("loser_pay4@test.com", "Password123!", "Loser 4");

        var auctionId = await SeedEndedAuctionWithWinnerAsync(winnerUserId, loserUserId, 300m, 600m);

        var res1 = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
        var p1 = await res1.Content.ReadFromJsonAsync<AuctionPaymentResponse>();

        var res2 = await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("CARD"));
        Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
        var p2 = await res2.Content.ReadFromJsonAsync<AuctionPaymentResponse>();

        Assert.NotNull(p1);
        Assert.NotNull(p2);
        Assert.Equal(p1.PaymentId, p2.PaymentId);
        Assert.Equal(p1.TransactionReference, p2.TransactionReference);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var paymentsCount = await db.AuctionPayments.CountAsync(p => p.AuctionId == auctionId);
        Assert.Equal(1, paymentsCount);
    }

    [Fact]
    public async Task Test05_Customer_Can_Retrieve_Payment_History()
    {
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("winner_pay5@test.com", "Password123!", "Winner 5");
        var (_, loserUserId) = await GetAuthenticatedCustomerClientAsync("loser_pay5@test.com", "Password123!", "Loser 5");

        var auctionId = await SeedEndedAuctionWithWinnerAsync(winnerUserId, loserUserId, 300m, 600m);
        await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));

        var historyRes = await winnerClient.GetAsync("/api/customer/payments");
        Assert.Equal(HttpStatusCode.OK, historyRes.StatusCode);

        var history = await historyRes.Content.ReadFromJsonAsync<List<CustomerPaymentHistoryResponse>>();
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal(9000m, history[0].TotalPayableAmount);
        Assert.Equal("PAID", history[0].PaymentStatus);
    }

    [Fact]
    public async Task Test06_Farmer_Can_Retrieve_Auction_Payment_Status()
    {
        var (farmerClient, farmerUserId) = await GetAuthenticatedFarmerClientAsync("farmer_pay6@test.com", "Password123!", "Farmer 6");
        var (winnerClient, winnerUserId) = await GetAuthenticatedCustomerClientAsync("winner_pay6@test.com", "Password123!", "Winner 6");

        var auctionId = await SeedEndedAuctionForFarmerAsync(farmerUserId, winnerUserId, 300m, 600m);

        // Before payment -> PENDING
        var check1 = await farmerClient.GetAsync($"/api/farmer/auctions/{auctionId}/payment");
        Assert.Equal(HttpStatusCode.OK, check1.StatusCode);

        // Process payment
        await winnerClient.PostAsJsonAsync($"/api/customer/auctions/{auctionId}/payments", new ProcessPaymentRequest("UPI"));

        // After payment -> PAID
        var check2 = await farmerClient.GetAsync($"/api/farmer/auctions/{auctionId}/payment");
        Assert.Equal(HttpStatusCode.OK, check2.StatusCode);
        var p = await check2.Content.ReadFromJsonAsync<AuctionPaymentResponse>();
        Assert.NotNull(p);
        Assert.Equal("PAID", p.PaymentStatus);
        Assert.Equal(9000m, p.TotalPayableAmount);
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

    private async Task<Guid> SeedEndedAuctionWithWinnerAsync(Guid winnerUserId, Guid loserUserId, decimal quantity, decimal winningBidRate)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"farmer_end_{emailSuffix}@test.com", "Password123!", "Farmer End");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();
        var winner = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);
        var loser = await db.CustomerProfiles.FirstAsync(c => c.UserId == loserUserId);

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Wheat Payment", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 1000, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = quantity, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = 25m,
            CurrentHighestBid = winningBidRate,
            MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bidLoser = new Bid { AuctionId = auction.Id, CustomerProfileId = loser.Id, Amount = 27m, BidTimeUtc = DateTime.UtcNow.AddHours(-4), BidStatus = BidStatus.Active };
        var bidWinner = new Bid { AuctionId = auction.Id, CustomerProfileId = winner.Id, Amount = winningBidRate, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.AddRange(bidLoser, bidWinner);
        await db.SaveChangesAsync();

        var auctionWinner = new AuctionWinner
        {
            AuctionId = auction.Id,
            CustomerProfileId = winner.Id,
            WinningBidId = bidWinner.Id,
            FinalAmount = winningBidRate,
            SelectedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.AuctionWinners.Add(auctionWinner);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedLiveAuctionAsync(Guid customerUserId, decimal quantity, decimal startingPrice)
    {
        var emailSuffix = Guid.NewGuid().ToString("N");
        await GetAuthenticatedFarmerClientAsync($"farmer_live_{emailSuffix}@test.com", "Password123!", "Farmer Live");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.OrderByDescending(f => f.CreatedAtUtc).FirstAsync();

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Live Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 1000, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = quantity, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = 0m,
            MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddMinutes(-30),
            EndTimeUtc = DateTime.UtcNow.AddHours(5),
            AuctionStatus = AuctionStatus.Live
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction.Id;
    }

    private async Task<Guid> SeedEndedAuctionForFarmerAsync(Guid farmerUserId, Guid winnerUserId, decimal quantity, decimal winningBidRate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.FirstAsync(f => f.UserId == farmerUserId);
        var winner = await db.CustomerProfiles.FirstAsync(c => c.UserId == winnerUserId);

        var crop = new Crop { FarmerProfileId = farmer.Id, CropName = "Farmer Paid Wheat", CropType = "Grain", Area = 5, AreaUnit = FarmSizeUnit.Acre, Status = CropStatus.Harvested, Quantity = 1000, Unit = MeasurementUnit.Kilogram };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing { FarmerProfileId = farmer.Id, Crop = crop, QuantityForSale = quantity, Unit = MeasurementUnit.Kilogram, ListingType = ListingType.Auction, ListingStatus = ListingStatus.Active };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = 25m,
            CurrentHighestBid = winningBidRate,
            MinimumBidIncrement = 2m,
            StartTimeUtc = DateTime.UtcNow.AddHours(-6),
            EndTimeUtc = DateTime.UtcNow.AddHours(-1),
            AuctionStatus = AuctionStatus.Ended
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        var bidWinner = new Bid { AuctionId = auction.Id, CustomerProfileId = winner.Id, Amount = winningBidRate, BidTimeUtc = DateTime.UtcNow.AddHours(-3), BidStatus = BidStatus.Active };
        db.Bids.Add(bidWinner);
        await db.SaveChangesAsync();

        var auctionWinner = new AuctionWinner
        {
            AuctionId = auction.Id,
            CustomerProfileId = winner.Id,
            WinningBidId = bidWinner.Id,
            FinalAmount = winningBidRate,
            SelectedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        db.AuctionWinners.Add(auctionWinner);
        await db.SaveChangesAsync();

        return auction.Id;
    }
}
