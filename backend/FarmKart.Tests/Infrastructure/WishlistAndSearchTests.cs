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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class WishlistAndSearchTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WishlistAndSearchTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WishlistSearchTest_{Guid.NewGuid()}";
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
                var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        db.Database.EnsureDeleted();
    }

    private async Task<(HttpClient Client, string UserId, Guid CustomerProfileId)> CreateAuthenticatedCustomerClientAsync(string email = "customer@wishlist.com")
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync(Roles.Customer))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));
            }

            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await authService.RegisterCustomerAsync(new CustomerRegisterRequest(
                "Wishlist Customer", email, "Password123!", "9988776655", null, "123 Customer Road"));
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        loginRes.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            var custProfile = await db.CustomerProfiles.FirstAsync(p => p.UserId == user.Id);
            return (client, user.Id.ToString(), custProfile.Id);
        }
    }

    private async Task<(Guid FarmerProfileId, Guid CropId, Guid AuctionId)> SeedCropAndAuctionAsync(
        string cropName = "Organic Wheat",
        string cropType = "Grain",
        decimal startingPrice = 500m,
        decimal quantityKg = 1000m,
        AuctionStatus status = AuctionStatus.Live,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmerUser = new ApplicationUser
        {
            UserName = $"farmer_{Guid.NewGuid()}@test.com",
            Email = $"farmer_{Guid.NewGuid()}@test.com",
            PhoneNumber = "1234567890"
        };
        db.Users.Add(farmerUser);
        await db.SaveChangesAsync();

        var farmerProfile = new FarmerProfile
        {
            UserId = farmerUser.Id,
            FullName = "Test Farmer",
            Phone = farmerUser.PhoneNumber ?? "1234567890",
            FarmName = "Green Fields",
            FarmLocation = "Surat, Gujarat"
        };
        db.FarmerProfiles.Add(farmerProfile);
        await db.SaveChangesAsync();

        var crop = new Crop
        {
            FarmerProfileId = farmerProfile.Id,
            CropName = cropName,
            CropType = cropType,
            Variety = "Lokwan",
            Quantity = quantityKg,
            Unit = MeasurementUnit.Kilogram,
            Status = CropStatus.ReadyForHarvest
        };
        db.Crops.Add(crop);
        await db.SaveChangesAsync();

        var listing = new CropListing
        {
            FarmerProfileId = farmerProfile.Id,
            CropId = crop.Id,
            QuantityForSale = quantityKg,
            Unit = MeasurementUnit.Kilogram,
            PricePerUnit = startingPrice,
            ListingType = ListingType.Auction,
            ListingStatus = ListingStatus.Active
        };
        db.CropListings.Add(listing);
        await db.SaveChangesAsync();

        var start = startTime ?? DateTime.UtcNow.AddHours(-1);
        var end = endTime ?? DateTime.UtcNow.AddHours(5);

        var auction = new Auction
        {
            CropListingId = listing.Id,
            FarmerProfileId = farmerProfile.Id,
            StartingPrice = startingPrice,
            CurrentHighestBid = startingPrice,
            MinimumBidIncrement = 10m,
            StartTimeUtc = start,
            EndTimeUtc = end,
            AuctionStatus = status
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();

        return (farmerProfile.Id, crop.Id, auction.Id);
    }

    #region Phase 8.2 Wishlist Tests

    [Fact]
    public async Task AddCropToWishlist_Succeeds()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c1@test.com");
        var (_, cropId, _) = await SeedCropAndAuctionAsync();

        var res = await client.PostAsJsonAsync("/api/customer/wishlist", new
        {
            ItemType = WishlistItemType.Crop,
            ItemId = cropId
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var item = await res.Content.ReadFromJsonAsync<WishlistItemResponse>();
        Assert.NotNull(item);
        Assert.Equal(cropId, item.ItemId);
        Assert.Equal(WishlistItemType.Crop, item.ItemType);
        Assert.Equal("Organic Wheat", item.CropName);
    }

    [Fact]
    public async Task AddAuctionToWishlist_Succeeds()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c2@test.com");
        var (_, _, auctionId) = await SeedCropAndAuctionAsync();

        var res = await client.PostAsJsonAsync("/api/customer/wishlist", new
        {
            ItemType = WishlistItemType.Auction,
            ItemId = auctionId
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var item = await res.Content.ReadFromJsonAsync<WishlistItemResponse>();
        Assert.NotNull(item);
        Assert.Equal(auctionId, item.ItemId);
        Assert.Equal(WishlistItemType.Auction, item.ItemType);
        Assert.Equal("LIVE", item.AuctionStatus);
    }

    [Fact]
    public async Task DuplicateWishlistAdd_IsIdempotent()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c3@test.com");
        var (_, cropId, _) = await SeedCropAndAuctionAsync();

        var res1 = await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Crop, ItemId = cropId });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Add second time
        var res2 = await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Crop, ItemId = cropId });
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);

        // Verify only 1 entry in list
        var listRes = await client.GetFromJsonAsync<List<WishlistItemResponse>>("/api/customer/wishlist");
        Assert.NotNull(listRes);
        Assert.Single(listRes);
    }

    [Fact]
    public async Task RemoveFromWishlist_RemovesEntry()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c4@test.com");
        var (_, cropId, _) = await SeedCropAndAuctionAsync();

        await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Crop, ItemId = cropId });

        var delRes = await client.DeleteAsync($"/api/customer/wishlist/1/{cropId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        var listRes = await client.GetFromJsonAsync<List<WishlistItemResponse>>("/api/customer/wishlist");
        Assert.NotNull(listRes);
        Assert.Empty(listRes);
    }

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomerWishlist()
    {
        var (client1, user1Id, _) = await CreateAuthenticatedCustomerClientAsync("user1@test.com");
        var (client2, user2Id, _) = await CreateAuthenticatedCustomerClientAsync("user2@test.com");
        var (_, cropId, _) = await SeedCropAndAuctionAsync();

        // Customer 1 adds item
        await client1.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Crop, ItemId = cropId });

        // Customer 2's wishlist should be empty
        var listRes2 = await client2.GetFromJsonAsync<List<WishlistItemResponse>>("/api/customer/wishlist");
        Assert.NotNull(listRes2);
        Assert.Empty(listRes2);

        // Customer 2 cannot remove Customer 1's wishlist item
        var delRes = await client2.DeleteAsync($"/api/customer/wishlist/1/{cropId}");
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var res = await unauthClient.GetAsync("/api/customer/wishlist");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task InvalidNonExistingItem_Returns404()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c5@test.com");
        var res = await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Crop, ItemId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task ExpiredAuction_CannotBeAddedToWishlist_And_AutoPurgedWhenEnded()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c6@test.com");
        var pastStart = DateTime.UtcNow.AddHours(-10);
        var pastEnd = DateTime.UtcNow.AddHours(-1);
        var (_, _, endedAuctionId) = await SeedCropAndAuctionAsync("Expired Crop", "Grain", 400m, 500m, AuctionStatus.Ended, pastStart, pastEnd);

        // 1. Cannot add ended auction to wishlist
        var addRes = await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Auction, ItemId = endedAuctionId });
        Assert.Equal(HttpStatusCode.BadRequest, addRes.StatusCode);

        // 2. Add a live auction that expires
        var (_, _, liveAuctionId) = await SeedCropAndAuctionAsync("Live Auction", "Grain");
        await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Auction, ItemId = liveAuctionId });

        var listBefore = await client.GetFromJsonAsync<List<WishlistItemResponse>>("/api/customer/wishlist");
        Assert.NotNull(listBefore);
        Assert.Single(listBefore);
    }

    [Fact]
    public async Task GetWishlistCount_ReturnsCorrectCounts()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("c7@test.com");
        var (_, cropId, auctionId) = await SeedCropAndAuctionAsync();

        await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Crop, ItemId = cropId });
        await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Auction, ItemId = auctionId });

        var countRes = await client.GetFromJsonAsync<WishlistCountResponse>("/api/customer/wishlist/count");
        Assert.NotNull(countRes);
        Assert.Equal(2, countRes.Total);
        Assert.Equal(1, countRes.CropCount);
        Assert.Equal(1, countRes.AuctionCount);
    }

    #endregion

    #region Phase 8.3 Advanced Search & Filter Tests

    [Fact]
    public async Task SearchByCropName_PartialAndCaseInsensitive()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s1@test.com");
        await SeedCropAndAuctionAsync("Premium Golden Wheat", "Grain");
        await SeedCropAndAuctionAsync("Red Tomatoes", "Vegetable");

        var paged = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?search=gold");
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("Premium Golden Wheat", paged.Items[0].CropName);
    }

    [Fact]
    public async Task PricePerManFilter_FiltersByStartingPrice()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s2@test.com");
        await SeedCropAndAuctionAsync("Cheap Wheat", "Grain", startingPrice: 300m);
        await SeedCropAndAuctionAsync("Mid Price Wheat", "Grain", startingPrice: 600m);
        await SeedCropAndAuctionAsync("Expensive Wheat", "Grain", startingPrice: 1200m);

        var paged = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?minPricePerMan=500&maxPricePerMan=800");
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("Mid Price Wheat", paged.Items[0].CropName);
    }

    [Fact]
    public async Task QuantityKgFilter_FiltersByAuctionQuantity()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s3@test.com");
        await SeedCropAndAuctionAsync("Small Batch", "Grain", quantityKg: 50m);
        await SeedCropAndAuctionAsync("Medium Batch", "Grain", quantityKg: 500m);
        await SeedCropAndAuctionAsync("Large Batch", "Grain", quantityKg: 5000m);

        var paged = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?minQuantityKg=100&maxQuantityKg=1000");
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("Medium Batch", paged.Items[0].CropName);
    }

    [Fact]
    public async Task EndingSoonFilter_ReturnsAuctionsEndingWithin24Hours()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s4@test.com");
        var now = DateTime.UtcNow;
        await SeedCropAndAuctionAsync("Ending In 5 Hours", "Grain", startTime: now.AddHours(-1), endTime: now.AddHours(5));
        await SeedCropAndAuctionAsync("Ending In 48 Hours", "Grain", startTime: now.AddHours(-1), endTime: now.AddHours(48));

        var paged = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?endingSoon=true");
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("Ending In 5 Hours", paged.Items[0].CropName);
    }

    [Fact]
    public async Task CombinedFilters_ApplyAndLogic()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s5@test.com");
        await SeedCropAndAuctionAsync("Target Wheat", "Grain", startingPrice: 600m, quantityKg: 300m);
        await SeedCropAndAuctionAsync("Target Rice", "Grain", startingPrice: 600m, quantityKg: 300m);
        await SeedCropAndAuctionAsync("Target Wheat Expensive", "Grain", startingPrice: 1500m, quantityKg: 300m);

        var paged = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?search=Wheat&category=Grain&minPricePerMan=500&maxPricePerMan=800&minQuantityKg=200");
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("Target Wheat", paged.Items[0].CropName);
    }

    [Fact]
    public async Task Pagination_ReturnsCorrectPageAndCount()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s6@test.com");
        for (int i = 1; i <= 5; i++)
        {
            await SeedCropAndAuctionAsync($"Batch {i}", "Grain");
        }

        var page1 = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalPages);

        var page3 = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?page=3&pageSize=2");
        Assert.NotNull(page3);
        Assert.Single(page3.Items);
    }

    [Fact]
    public async Task SearchResponse_InjectsIsFavoritedState()
    {
        var (client, _, _) = await CreateAuthenticatedCustomerClientAsync("s7@test.com");
        var (_, _, auctionId) = await SeedCropAndAuctionAsync("Favorited Crop", "Grain");

        // Customer favorites the auction
        await client.PostAsJsonAsync("/api/customer/wishlist", new { ItemType = WishlistItemType.Auction, ItemId = auctionId });

        // Get marketplace auctions as this customer
        var paged = await client.GetFromJsonAsync<PagedCustomerAuctionResponse>("/api/customer/auctions?search=Favorited");
        Assert.NotNull(paged);
        var item = Assert.Single(paged.Items);
        Assert.True(item.IsFavorited);
    }

    #endregion
}
