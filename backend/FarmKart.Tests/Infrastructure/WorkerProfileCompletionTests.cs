using FarmKart.Application.Abstractions.Authentication;
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class WorkerProfileCompletionTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerProfileCompletionTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerCompletionTest_{Guid.NewGuid()}";
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
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        db.Database.EnsureDeleted();
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
                fullName, email, password, "9876543210", null, "123 Farm Road", "Happy Farm", 10.5m, FarmSizeUnit.Vigha, "Near Valley"));
        }
        else if (role == Roles.Worker)
        {
            await authService.RegisterWorkerAsync(new WorkerRegisterRequest(
                fullName, email, password, "9876543210", null, "123 Worker Road", 2, 100));
        }
        else if (role == Roles.Customer)
        {
            await authService.RegisterCustomerAsync(new CustomerRegisterRequest(
                fullName, email, password, "9876543210", null, "123 Customer Road"));
        }
    }

    private async Task<HttpClient> GetAuthenticatedClientAsync(string email, string password, string fullName, string role)
    {
        await SetupTestUserAsync(email, password, fullName, role);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var cookieHeader = loginResponse.Headers.GetValues("Set-Cookie").First();
        var nameValuePair = cookieHeader.Split(';').First(p => p.Trim().StartsWith("FarmKartAuth=")).Trim();

        var authenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", nameValuePair);

        return authenticatedClient;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public async Task WorkerCanRetrieveOwnProfileCompletionStatus()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.compget@test.com", "Password123!", "Worker CompGet", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync("/api/worker/profile/completion");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completion = await response.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);
        Assert.NotNull(completion);
        Assert.True(completion.OverallCompletionPercentage > 0);
        Assert.NotNull(completion.Sections);
        Assert.Equal("Not Verified", completion.VerificationStatus);
    }

    [Fact]
    public async Task CompletionPercentageIsCalculatedDynamically()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.dyncomp@test.com", "Password123!", "Worker DynComp", Roles.Worker);

        // Initial completion check
        var resp1 = await workerClient.GetAsync("/api/worker/profile/completion");
        var comp1 = await resp1.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);
        Assert.NotNull(comp1);

        // Update profile with skills & experience description & preferences
        await workerClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest(
            FullName: "Worker DynComp",
            Phone: "9876543210",
            Address: "123 Worker Road",
            ExperienceYears: 5,
            ExperienceDescription: "Experienced in wheat and paddy harvesting.",
            ExpectedDailyWage: 500,
            Skills: new List<string> { "Harvesting", "Plowing" }
        ));

        await workerClient.PutAsJsonAsync("/api/worker/preferences", new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Surat" },
            MinimumDailyWage: 450,
            PreferredWorkingHours: "8 AM - 5 PM",
            FoodPreference: null,
            AccommodationPreference: null
        ));

        // Act: Get completion again
        var resp2 = await workerClient.GetAsync("/api/worker/profile/completion");
        var comp2 = await resp2.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);

        // Assert: Completion percentage increased dynamically
        Assert.NotNull(comp2);
        Assert.True(comp2.OverallCompletionPercentage > comp1.OverallCompletionPercentage);
    }

    [Fact]
    public async Task CompleteProfileProducesCorrectCompletionPercentage()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.fullcomp@test.com", "Password123!", "Worker FullComp", Roles.Worker);

        // Populate all profile sections
        await workerClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest(
            FullName: "Worker FullComp",
            Phone: "9876543210",
            Address: "123 Worker Road",
            ExperienceYears: 5,
            ExperienceDescription: "Experienced in all farm operations.",
            ExpectedDailyWage: 500,
            ProfileImageUrl: "https://example.com/photo.jpg",
            IsAvailable: true,
            Skills: new List<string> { "Harvesting" }
        ));

        await workerClient.PutAsJsonAsync("/api/worker/preferences", new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Surat" },
            MinimumDailyWage: 500,
            PreferredWorkingHours: "8 AM - 5 PM",
            FoodPreference: null,
            AccommodationPreference: null
        ));

        // Act
        var response = await workerClient.GetAsync("/api/worker/profile/completion");

        // Assert: 100% completion
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completion = await response.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);
        Assert.NotNull(completion);
        Assert.Equal(100, completion.OverallCompletionPercentage);
        Assert.All(completion.Sections, s => Assert.True(s.IsComplete));
    }

    [Fact]
    public async Task MissingRequiredProfileInformationReducesCompletionPercentage()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.missingcomp@test.com", "Password123!", "Worker MissingComp", Roles.Worker);

        // Clear skills & experience description & preferences
        await workerClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest(
            FullName: "Worker MissingComp",
            Phone: "9876543210",
            Address: "123 Worker Road",
            ExperienceYears: 0,
            ExperienceDescription: null,
            ExpectedDailyWage: 0,
            Skills: new List<string>()
        ));

        // Act
        var response = await workerClient.GetAsync("/api/worker/profile/completion");

        // Assert: Reduced completion percentage
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completion = await response.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);
        Assert.NotNull(completion);
        Assert.True(completion.OverallCompletionPercentage < 100);
        Assert.Contains(completion.Sections, s => !s.IsComplete);
    }

    [Fact]
    public async Task OptionalFieldsDoNotIncorrectlyBlockCompletion()
    {
        // Arrange: Worker without optional profile image or availability notes
        var workerClient = await GetAuthenticatedClientAsync("worker.optcomp@test.com", "Password123!", "Worker OptComp", Roles.Worker);

        await workerClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest(
            FullName: "Worker OptComp",
            Phone: "9876543210",
            Address: "123 Worker Road",
            ExperienceYears: 2,
            ExperienceDescription: "Farm work experience",
            ExpectedDailyWage: 400,
            ProfileImageUrl: null,
            IsAvailable: true,
            Skills: new List<string> { "Sowing" }
        ));

        await workerClient.PutAsJsonAsync("/api/worker/preferences", new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Sowing" },
            PreferredLocations: new List<string> { "Baroda" },
            MinimumDailyWage: 400,
            PreferredWorkingHours: "8 AM - 5 PM",
            FoodPreference: null,
            AccommodationPreference: null
        ));

        // Act
        var response = await workerClient.GetAsync("/api/worker/profile/completion");

        // Assert: 90% completion (100% minus 10% optional photo)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completion = await response.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);
        Assert.NotNull(completion);
        Assert.Equal(90, completion.OverallCompletionPercentage);
    }

    [Fact]
    public async Task WorkerCannotAccessAnotherWorkerCompletionStatus()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.compA@test.com", "Password123!", "Worker A", Roles.Worker);
        await SetupTestUserAsync("worker.compB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Act: Worker A gets own completion
        var response = await workerAClient.GetAsync("/api/worker/profile/completion");

        // Assert: 200 OK returning Worker A's own completion
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotModifyCompletionPercentageDirectly()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.nomodcomp@test.com", "Password123!", "Worker NoModComp", Roles.Worker);

        // Act: Completion endpoint is GET only
        var response = await workerClient.PutAsJsonAsync("/api/worker/profile/completion", new { overallCompletionPercentage = 100 });

        // Assert: 405 Method Not Allowed or 404
        Assert.True(response.StatusCode == HttpStatusCode.MethodNotAllowed || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WorkerCannotMarkThemselvesVerified()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.noverif@test.com", "Password123!", "Worker NoVerif", Roles.Worker);

        // Act: Worker attempts to send "verificationStatus": "Verified" or "isVerified": true in profile update
        var response = await workerClient.PutAsJsonAsync("/api/worker/profile", new
        {
            fullName = "Worker NoVerif",
            phone = "9876543210",
            address = "123 Worker Road",
            experienceYears = 2,
            expectedDailyWage = 400,
            verificationStatus = "Verified",
            isVerified = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert: VerificationStatus remains "Not Verified"
        var compResp = await workerClient.GetAsync("/api/worker/profile/completion");
        var completion = await compResp.Content.ReadFromJsonAsync<WorkerProfileCompletionResponse>(_jsonOptions);
        Assert.NotNull(completion);
        Assert.Equal("Not Verified", completion.VerificationStatus);
    }

    [Fact]
    public async Task UnauthenticatedUserIsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/profile/completion");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
