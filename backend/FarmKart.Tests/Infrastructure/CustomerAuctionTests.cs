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

public class CustomerAuctionTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public CustomerAuctionTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_CustomerAuctionTest_{Guid.NewGuid()}";
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
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

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

    private async Task SetupTestUserAsync(string email, string password, string fullName, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        if (role == Roles.Farmer)
        {
            await authService.RegisterFarmerAsync(new FarmerRegisterRequest(
                fullName, email, password, "1234567890", null, "123 Farm Road", "Sunny Farm", 10.5m, FarmSizeUnit.Vigha, "Surat, Gujarat"));
        }
        else if (role == Roles.Worker)
        {
            await authService.RegisterWorkerAsync(new WorkerRegisterRequest(
                fullName, email, password, "1234567890", null, "123 Worker Road", 2, 100));
        }
        else if (role == Roles.Customer)
        {
            await authService.RegisterCustomerAsync(new CustomerRegisterRequest(
                fullName, email, password, "1234567890", null, "123 Customer Road"));
        }
    }

    private async Task<HttpClient> GetAuthenticatedClientAsync(string email, string password, string fullName, string role)
    {
        await SetupTestUserAsync(email, password, fullName, role);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return client;
    }

    private async Task<Auction> SeedAuctionAsync(string cropName, string cropType, string variety, decimal quantity, MeasurementUnit unit, decimal startingPrice, DateTime start, DateTime end, AuctionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmer = await db.FarmerProfiles.FirstOrDefaultAsync();
        if (farmer == null)
        {
            await SetupTestUserAsync("seed_farmer@test.com", "Password123!", "Seed Farmer", Roles.Farmer);
            farmer = await db.FarmerProfiles.FirstAsync();
        }

        var crop = new Crop
        {
            FarmerProfileId = farmer.Id,
            CropName = cropName,
            CropType = cropType,
            Variety = variety,
            Area = 5,
            AreaUnit = FarmSizeUnit.Acre,
            Status = CropStatus.Harvested,
            Quantity = 2000,
            Unit = MeasurementUnit.Kilogram
        };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing
        {
            CropId = crop.Id,
            FarmerProfileId = farmer.Id,
            QuantityForSale = quantity,
            Unit = unit,
            ListingType = ListingType.Auction,
            ListingStatus = ListingStatus.Active,
            Description = $"Fresh harvest of {cropName}"
        };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmer.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = 0m,
            MinimumBidIncrement = 5m,
            StartTimeUtc = start,
            EndTimeUtc = end,
            AuctionStatus = status
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return auction;
    }

    [Fact]
    public async Task Test01_Customer_Can_Get_Marketplace_Auctions()
    {
        var client = await GetAuthenticatedClientAsync("cust1@test.com", "Password123!", "Customer One", Roles.Customer);
        await SeedAuctionAsync("Basmati Rice", "Grain", "Super", 500, MeasurementUnit.Kilogram, 40, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddHours(2), AuctionStatus.Live);

        var res = await client.GetAsync("/api/customer/auctions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>();
        Assert.NotNull(list);
        Assert.NotEmpty(list);

        var item = list.FirstOrDefault(a => a.CropName == "Basmati Rice");
        Assert.NotNull(item);
        Assert.Equal("LIVE", item.Status);
        Assert.Equal(40m, item.StartingBidPrice);
    }

    [Fact]
    public async Task Test02_Cancelled_And_Draft_Auctions_Are_Excluded()
    {
        var client = await GetAuthenticatedClientAsync("cust2@test.com", "Password123!", "Customer Two", Roles.Customer);
        await SeedAuctionAsync("Cancelled Wheat", "Grain", "Lokwan", 100, MeasurementUnit.Kilogram, 20, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), AuctionStatus.Cancelled);

        var res = await client.GetAsync("/api/customer/auctions");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>();
        Assert.NotNull(list);
        Assert.DoesNotContain(list, a => a.CropName == "Cancelled Wheat");
    }

    [Fact]
    public async Task Test03_Search_Filter_Works()
    {
        var client = await GetAuthenticatedClientAsync("cust3@test.com", "Password123!", "Customer Three", Roles.Customer);
        await SeedAuctionAsync("Golden Mango", "Fruit", "Alphonso", 200, MeasurementUnit.Kilogram, 150, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddDays(1), AuctionStatus.Scheduled);

        var res = await client.GetAsync("/api/customer/auctions?search=Mango");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("Golden Mango", list[0].CropName);
    }

    [Fact]
    public async Task Test04_Category_Filter_Works()
    {
        var client = await GetAuthenticatedClientAsync("cust4@test.com", "Password123!", "Customer Four", Roles.Customer);
        await SeedAuctionAsync("Fresh Tomato", "Vegetable", "Hybrid", 100, MeasurementUnit.Kilogram, 15, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(3), AuctionStatus.Live);

        var res = await client.GetAsync("/api/customer/auctions?category=Vegetable");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>();
        Assert.NotNull(list);
        Assert.Contains(list, a => a.CropName == "Fresh Tomato");
    }

    [Fact]
    public async Task Test05_Status_Filter_Works()
    {
        var client = await GetAuthenticatedClientAsync("cust5@test.com", "Password123!", "Customer Five", Roles.Customer);
        await SeedAuctionAsync("Upcoming Corn", "Grain", "Sweet", 300, MeasurementUnit.Kilogram, 30, DateTime.UtcNow.AddHours(5), DateTime.UtcNow.AddDays(2), AuctionStatus.Scheduled);

        var res = await client.GetAsync("/api/customer/auctions?status=UPCOMING");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var list = await res.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>();
        Assert.NotNull(list);
        Assert.Contains(list, a => a.CropName == "Upcoming Corn" && a.Status == "UPCOMING");
    }

    [Fact]
    public async Task Test06_Get_Auction_By_Id_Returns_Details()
    {
        var client = await GetAuthenticatedClientAsync("cust6@test.com", "Password123!", "Customer Six", Roles.Customer);
        var auction = await SeedAuctionAsync("Detail Cotton", "Cash Crop", "Bt", 10, MeasurementUnit.Quintal, 5000, DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddHours(5), AuctionStatus.Live);

        var res = await client.GetAsync($"/api/customer/auctions/{auction.Id}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var detail = await res.Content.ReadFromJsonAsync<CustomerAuctionResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Detail Cotton", detail.CropName);
        Assert.Equal("LIVE", detail.Status);
    }

    [Fact]
    public async Task Test07_Get_Auction_By_Invalid_Id_Returns_404()
    {
        var client = await GetAuthenticatedClientAsync("cust7@test.com", "Password123!", "Customer Seven", Roles.Customer);

        var res = await client.GetAsync($"/api/customer/auctions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Test08_Unauthenticated_Request_Is_Rejected()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/customer/auctions");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Test09_Farmer_And_Worker_Cannot_Access_Customer_Marketplace()
    {
        var farmerClient = await GetAuthenticatedClientAsync("farmer_mp@test.com", "Password123!", "Farmer MP", Roles.Farmer);
        var resFarmer = await farmerClient.GetAsync("/api/customer/auctions");
        Assert.Equal(HttpStatusCode.Forbidden, resFarmer.StatusCode);

        var workerClient = await GetAuthenticatedClientAsync("worker_mp@test.com", "Password123!", "Worker MP", Roles.Worker);
        var resWorker = await workerClient.GetAsync("/api/customer/auctions");
        Assert.Equal(HttpStatusCode.Forbidden, resWorker.StatusCode);
    }
}
