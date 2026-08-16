using FarmKart.Application.DTOs;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class FarmerProfileAndMachineryReviewTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string FarmerRole = "Farmer";
    private const string CustomerRole = "Customer";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FarmerProfileAndMachineryReviewTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_ProfileReviewTest_{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Secret", "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!" },
                    { "JwtSettings:Issuer", "FarmKart" },
                    { "JwtSettings:Audience", "FarmKartUsers" },
                    { "JwtSettings:ExpiryMinutes", "120" }
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<FarmKartDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<FarmKartDbContext>(options =>
                    options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True"));

                using var sp = services.BuildServiceProvider();
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

    private async Task<(HttpClient Client, Guid UserId)> GetAuthenticatedClientAsync(string email, string role)
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "Password123!");
            await userManager.AddToRoleAsync(user, role);

            if (role == FarmerRole)
            {
                db.FarmerProfiles.Add(new FarmKart.Domain.Entities.FarmerProfile
                {
                    UserId = user.Id,
                    FullName = "Test Farmer " + user.Id.ToString()[..4],
                    Phone = "9876543210"
                });
            }
            else if (role == CustomerRole)
            {
                db.CustomerProfiles.Add(new FarmKart.Domain.Entities.CustomerProfile
                {
                    UserId = user.Id,
                    FullName = "Test Customer " + user.Id.ToString()[..4],
                    Phone = "9123456789"
                });
            }
            await db.SaveChangesAsync();
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        loginRes.EnsureSuccessStatusCode();

        return (client, user.Id);
    }

    [Fact]
    public async Task GetPublicProfile_ExistingFarmer_ReturnsProfileInformation()
    {
        var (farmerClient, farmerUserId) = await GetAuthenticatedClientAsync($"farmer_prof_{Guid.NewGuid()}@test.com", FarmerRole);
        var (customerClient, _) = await GetAuthenticatedClientAsync($"cust_prof_{Guid.NewGuid()}@test.com", CustomerRole);

        // Add a machinery for the farmer
        var machRes = await farmerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "John Deere 5050D", Category: "Tractor", Brand: "John Deere", Model: "5050D",
            ManufacturingYear: 2023, Description: "50 HP Heavy Tractor", DailyRent: 2000, SecurityDeposit: 500,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: true, DriverChargePerDay: 500,
            Location: "Anand Road", City: "Anand", State: "Gujarat", Pincode: "388001"
        ));
        machRes.EnsureSuccessStatusCode();

        var profileRes = await customerClient.GetAsync($"/api/farmers/{farmerUserId}/profile");
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);

        var profile = await profileRes.Content.ReadFromJsonAsync<FarmerPublicProfileResponse>(JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal(farmerUserId, profile.UserId);
        Assert.Single(profile.Machinery);
        Assert.Equal("John Deere 5050D", profile.Machinery[0].Name);
    }

    [Fact]
    public async Task GetPublicProfile_NonExistentFarmer_Returns404NotFound()
    {
        var (client, _) = await GetAuthenticatedClientAsync($"cust_bad_{Guid.NewGuid()}@test.com", CustomerRole);
        var res = await client.GetAsync($"/api/farmers/{Guid.NewGuid()}/profile");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MachineryReview_CompletedRentalRenter_CanSubmitReview()
    {
        var (ownerClient, ownerUserId) = await GetAuthenticatedClientAsync($"owner_rev_{Guid.NewGuid()}@test.com", FarmerRole);
        var (renterClient, renterUserId) = await GetAuthenticatedClientAsync($"renter_rev_{Guid.NewGuid()}@test.com", CustomerRole);

        // Create Machinery
        var machRes = await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "Mahindra Harvester", Category: "Harvester", Brand: "Mahindra", Model: "M1",
            ManufacturingYear: 2022, Description: "Desc", DailyRent: 3000, SecurityDeposit: 1000,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: false, DriverChargePerDay: 0,
            Location: "Loc", City: "Surat", State: "Gujarat", Pincode: "395007"
        ));
        machRes.EnsureSuccessStatusCode();
        var machinery = await machRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        // Book Machinery with StartDate = Today
        var bookRes = await renterClient.PostAsJsonAsync($"/api/machinery/{machinery!.Id}/rentals", new BookRentalRequest(
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            DriverRequired: false,
            PaymentMethod: "Cash"
        ));
        bookRes.EnsureSuccessStatusCode();
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        // Advance to Completed
        await ownerClient.PatchAsync($"/api/rentals/{rental!.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Confirmed")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("ReadyForHandover")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("RentedOut")));
        await renterClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Returned")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Completed")));

        // Renter submits review
        var revRes = await renterClient.PostAsJsonAsync($"/api/rentals/{rental.Id}/review", new CreateMachineryReviewRequest(
            Rating: 5,
            Comment: "Excellent performance and smooth transaction!"
        ));
        Assert.Equal(HttpStatusCode.Created, revRes.StatusCode);

        var review = await revRes.Content.ReadFromJsonAsync<MachineryReviewResponse>(JsonOptions);
        Assert.NotNull(review);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Excellent performance and smooth transaction!", review.Comment);

        // Check machinery ratings summary
        var summaryRes = await renterClient.GetAsync($"/api/machinery/{machinery.Id}/reviews");
        var summary = await summaryRes.Content.ReadFromJsonAsync<MachineryRatingSummaryResponse>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(5.0, summary.AverageRating);
        Assert.Equal(1, summary.TotalReviews);
    }

    [Fact]
    public async Task GetOwnerMachineryReviews_OwnerAccess_Succeeds_AndNonOwner_ReturnsForbidden()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"owner_auth_{Guid.NewGuid()}@test.com", CustomerRole);
        var (otherClient, _) = await GetAuthenticatedClientAsync($"other_user_{Guid.NewGuid()}@test.com", CustomerRole);

        var machRes = await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "Owner Tractor", Category: "Tractor", Brand: "Mahindra", Model: "M55",
            ManufacturingYear: 2023, Description: "Desc", DailyRent: 2500, SecurityDeposit: 500,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: false, DriverChargePerDay: 0,
            Location: "Loc", City: "Vadora", State: "Gujarat", Pincode: "390001"
        ));
        machRes.EnsureSuccessStatusCode();
        var machinery = await machRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        // Owner gets owner reviews
        var ownerRevRes = await ownerClient.GetAsync($"/api/my-machinery/{machinery!.Id}/reviews");
        Assert.Equal(HttpStatusCode.OK, ownerRevRes.StatusCode);

        // Non-owner attempts to get private owner reviews
        var otherRevRes = await otherClient.GetAsync($"/api/my-machinery/{machinery.Id}/reviews");
        Assert.Equal(HttpStatusCode.Forbidden, otherRevRes.StatusCode);
    }

    [Fact]
    public async Task GetUnifiedMyReviews_ReturnsSeparatedCropAndMachineryReviews()
    {
        var (renterClient, renterUserId) = await GetAuthenticatedClientAsync($"renter_myrev_{Guid.NewGuid()}@test.com", FarmerRole);
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"owner_myrev_{Guid.NewGuid()}@test.com", CustomerRole);

        // Machinery rental & review
        var machRes = await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "Power Tiller", Category: "Tractor", Brand: "B1", Model: "M1",
            ManufacturingYear: 2022, Description: "Desc", DailyRent: 1000, SecurityDeposit: 200,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: false, DriverChargePerDay: 0,
            Location: "Loc", City: "Surat", State: "Gujarat", Pincode: "395007"
        ));
        machRes.EnsureSuccessStatusCode();
        var machinery = await machRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var bookRes = await renterClient.PostAsJsonAsync($"/api/machinery/{machinery!.Id}/rentals", new BookRentalRequest(
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DriverRequired: false,
            PaymentMethod: "Cash"
        ));
        bookRes.EnsureSuccessStatusCode();
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        await ownerClient.PatchAsync($"/api/rentals/{rental!.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Confirmed")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("ReadyForHandover")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("RentedOut")));
        await renterClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Returned")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Completed")));

        await renterClient.PostAsJsonAsync($"/api/rentals/{rental.Id}/review", new CreateMachineryReviewRequest(5, "Great machinery!"));

        // Call Unified My Reviews
        var myRevRes = await renterClient.GetAsync("/api/my-reviews");
        Assert.Equal(HttpStatusCode.OK, myRevRes.StatusCode);

        var myReviews = await myRevRes.Content.ReadFromJsonAsync<UserMyReviewsSummaryResponse>(JsonOptions);
        Assert.NotNull(myReviews);
        Assert.True(myReviews.TotalCount >= 1);
        Assert.True(myReviews.MachineryCount >= 1);
        Assert.Contains(myReviews.MachineryReviews, r => r.ReviewType == "MACHINERY" && r.MachineryName == "Power Tiller");
    }

    [Fact]
    public async Task MachineryReview_IncompleteRental_Rejected()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"owner_inc_{Guid.NewGuid()}@test.com", FarmerRole);
        var (renterClient, _) = await GetAuthenticatedClientAsync($"renter_inc_{Guid.NewGuid()}@test.com", CustomerRole);

        var machRes = await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "Mini Cultivator", Category: "Cultivator", Brand: "B1", Model: "M1",
            ManufacturingYear: 2021, Description: "Desc", DailyRent: 800, SecurityDeposit: 200,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: false, DriverChargePerDay: 0,
            Location: "Loc", City: "Rajkot", State: "Gujarat", Pincode: "360005"
        ));
        machRes.EnsureSuccessStatusCode();
        var machinery = await machRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var bookRes = await renterClient.PostAsJsonAsync($"/api/machinery/{machinery!.Id}/rentals", new BookRentalRequest(
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DriverRequired: false,
            PaymentMethod: "Cash"
        ));
        bookRes.EnsureSuccessStatusCode();
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        // Rental is still 'Booked' (not Completed)
        var revRes = await renterClient.PostAsJsonAsync($"/api/rentals/{rental!.Id}/review", new CreateMachineryReviewRequest(
            Rating: 4,
            Comment: "Good tool!"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, revRes.StatusCode);
    }

    [Fact]
    public async Task MachineryReview_DuplicateReview_Rejected()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"owner_dup_{Guid.NewGuid()}@test.com", FarmerRole);
        var (renterClient, _) = await GetAuthenticatedClientAsync($"renter_dup_{Guid.NewGuid()}@test.com", CustomerRole);

        var machRes = await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "Tractor B", Category: "Tractor", Brand: "B2", Model: "M2",
            ManufacturingYear: 2022, Description: "Desc", DailyRent: 1500, SecurityDeposit: 300,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: false, DriverChargePerDay: 0,
            Location: "Loc", City: "Rajkot", State: "Gujarat", Pincode: "360005"
        ));
        machRes.EnsureSuccessStatusCode();
        var machinery = await machRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var bookRes = await renterClient.PostAsJsonAsync($"/api/machinery/{machinery!.Id}/rentals", new BookRentalRequest(
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DriverRequired: false,
            PaymentMethod: "Cash"
        ));
        bookRes.EnsureSuccessStatusCode();
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        // Complete rental
        await ownerClient.PatchAsync($"/api/rentals/{rental!.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Confirmed")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("ReadyForHandover")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("RentedOut")));
        await renterClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Returned")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Completed")));

        // First review
        var rev1 = await renterClient.PostAsJsonAsync($"/api/rentals/{rental.Id}/review", new CreateMachineryReviewRequest(4, "Great tractor!"));
        Assert.Equal(HttpStatusCode.Created, rev1.StatusCode);

        // Duplicate review attempt
        var rev2 = await renterClient.PostAsJsonAsync($"/api/rentals/{rental.Id}/review", new CreateMachineryReviewRequest(5, "Trying again!"));
        Assert.Equal(HttpStatusCode.BadRequest, rev2.StatusCode);
    }

    [Fact]
    public async Task MachineryReview_NonRenter_Rejected()
    {
        var (ownerClient, _) = await GetAuthenticatedClientAsync($"owner_non_{Guid.NewGuid()}@test.com", FarmerRole);
        var (renterClient, _) = await GetAuthenticatedClientAsync($"renter_non_{Guid.NewGuid()}@test.com", CustomerRole);
        var (intruderClient, _) = await GetAuthenticatedClientAsync($"intruder_{Guid.NewGuid()}@test.com", CustomerRole);

        var machRes = await ownerClient.PostAsJsonAsync("/api/my-machinery", new CreateMachineryRequest(
            Name: "Sprayer", Category: "Sprayer", Brand: "B3", Model: "M3",
            ManufacturingYear: 2022, Description: "Desc", DailyRent: 600, SecurityDeposit: 100,
            IsDriverIncluded: false, IsFuelIncluded: false, DriverAvailable: false, DriverChargePerDay: 0,
            Location: "Loc", City: "Jamnagar", State: "Gujarat", Pincode: "361001"
        ));
        machRes.EnsureSuccessStatusCode();
        var machinery = await machRes.Content.ReadFromJsonAsync<MachineryResponse>(JsonOptions);

        var bookRes = await renterClient.PostAsJsonAsync($"/api/machinery/{machinery!.Id}/rentals", new BookRentalRequest(
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DriverRequired: false,
            PaymentMethod: "Cash"
        ));
        bookRes.EnsureSuccessStatusCode();
        var rental = await bookRes.Content.ReadFromJsonAsync<MachineryRentalResponse>(JsonOptions);

        await ownerClient.PatchAsync($"/api/rentals/{rental!.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Confirmed")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("ReadyForHandover")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("RentedOut")));
        await renterClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Returned")));
        await ownerClient.PatchAsync($"/api/rentals/{rental.Id}/status", JsonContent.Create(new UpdateRentalStatusRequest("Completed")));

        // Intruder attempts to review
        var revRes = await intruderClient.PostAsJsonAsync($"/api/rentals/{rental.Id}/review", new CreateMachineryReviewRequest(5, "Fake review!"));
        Assert.Equal(HttpStatusCode.Forbidden, revRes.StatusCode);
    }
}
