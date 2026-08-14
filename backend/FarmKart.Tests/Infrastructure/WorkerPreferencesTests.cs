using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
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

public class WorkerPreferencesTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerPreferencesTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerPrefTest_{Guid.NewGuid()}";
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
    public async Task WorkerCanGetOwnPreferences()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.getpref@test.com", "Password123!", "Worker Pref", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/worker/preferences");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.NotNull(pref.PreferredWorkCategories);
        Assert.NotNull(pref.PreferredLocations);
    }

    [Fact]
    public async Task WorkerCanUpdateOwnPreferences()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.updatepref@test.com", "Password123!", "Worker UpPref", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting", "Sowing" },
            PreferredLocations: new List<string> { "Nadiad", "Anand" },
            MinimumDailyWage: 450,
            PreferredWorkingHours: "08:00 AM - 05:00 PM",
            FoodPreference: "Preferred",
            AccommodationPreference: "Not Required"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal(2, pref.PreferredWorkCategories.Count);
        Assert.Equal(2, pref.PreferredLocations.Count);
        Assert.Equal(450, pref.MinimumDailyWage);
        Assert.Equal("08:00 AM - 05:00 PM", pref.PreferredWorkingHours);
        Assert.Equal("Preferred", pref.FoodPreference);
        Assert.Equal("Not Required", pref.AccommodationPreference);
    }

    [Fact]
    public async Task MultipleCategoriesSaved()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.multicats@test.com", "Password123!", "Worker MultiCats", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting", "Sowing", "Irrigation", "Crop Maintenance" },
            PreferredLocations: new List<string> { "Nadiad" },
            MinimumDailyWage: 500,
            PreferredWorkingHours: "07:00 AM - 04:00 PM",
            FoodPreference: "Not Required",
            AccommodationPreference: "Not Required"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal(4, pref.PreferredWorkCategories.Count);
        Assert.Contains("Harvesting", pref.PreferredWorkCategories);
        Assert.Contains("Sowing", pref.PreferredWorkCategories);
        Assert.Contains("Irrigation", pref.PreferredWorkCategories);
        Assert.Contains("Crop Maintenance", pref.PreferredWorkCategories);
    }

    [Fact]
    public async Task DuplicateCategoriesPrevented()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.dupcats@test.com", "Password123!", "Worker DupCats", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting", "harvesting", "HARVESTING", "Sowing" },
            PreferredLocations: new List<string> { "Nadiad", "nadiad" },
            MinimumDailyWage: 500,
            PreferredWorkingHours: "08:00 AM - 05:00 PM",
            FoodPreference: "Preferred",
            AccommodationPreference: "Required"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal(2, pref.PreferredWorkCategories.Count);
        Assert.Equal(1, pref.PreferredLocations.Count);
    }

    [Fact]
    public async Task PreferredLocationsSaved()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.locs@test.com", "Password123!", "Worker Locs", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Nadiad", "Anand", "Kheda" },
            MinimumDailyWage: 400,
            PreferredWorkingHours: null,
            FoodPreference: null,
            AccommodationPreference: null
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal(3, pref.PreferredLocations.Count);
        Assert.Contains("Nadiad", pref.PreferredLocations);
        Assert.Contains("Anand", pref.PreferredLocations);
        Assert.Contains("Kheda", pref.PreferredLocations);
    }

    [Fact]
    public async Task MinimumWageSaved()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.minwage@test.com", "Password123!", "Worker MinWage", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Sowing" },
            PreferredLocations: new List<string> { "Anand" },
            MinimumDailyWage: 650.50m,
            PreferredWorkingHours: null,
            FoodPreference: null,
            AccommodationPreference: null
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal(650.50m, pref.MinimumDailyWage);
    }

    [Fact]
    public async Task NegativeWageRejected()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.negwage@test.com", "Password123!", "Worker NegWage", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Nadiad" },
            MinimumDailyWage: -100m,
            PreferredWorkingHours: null,
            FoodPreference: null,
            AccommodationPreference: null
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WorkingHoursSaved()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.hours@test.com", "Password123!", "Worker Hours", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Irrigation" },
            PreferredLocations: new List<string> { "Kheda" },
            MinimumDailyWage: 400,
            PreferredWorkingHours: "06:00 AM - 02:00 PM",
            FoodPreference: null,
            AccommodationPreference: null
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal("06:00 AM - 02:00 PM", pref.PreferredWorkingHours);
    }

    [Fact]
    public async Task FoodPreferenceSaved()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.food@test.com", "Password123!", "Worker Food", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Nadiad" },
            MinimumDailyWage: 400,
            PreferredWorkingHours: null,
            FoodPreference: "Preferred",
            AccommodationPreference: null
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal("Preferred", pref.FoodPreference);
    }

    [Fact]
    public async Task AccommodationPreferenceSaved()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.accom@test.com", "Password123!", "Worker Accom", Roles.Worker);
        var req = new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Nadiad" },
            MinimumDailyWage: 400,
            PreferredWorkingHours: null,
            FoodPreference: null,
            AccommodationPreference: "Required"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/preferences", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pref = await response.Content.ReadFromJsonAsync<WorkerPreferencesResponse>(_jsonOptions);
        Assert.NotNull(pref);
        Assert.Equal("Required", pref.AccommodationPreference);
    }

    [Fact]
    public async Task WorkerCannotModifyAnotherWorkerPreferences()
    {
        // Arrange: Worker A
        var workerAClient = await GetAuthenticatedClientAsync("worker.prefA@test.com", "Password123!", "Worker A", Roles.Worker);

        // Worker B
        await SetupTestUserAsync("worker.prefB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Worker A updates own preferences
        await workerAClient.PutAsJsonAsync("/api/worker/preferences", new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Harvesting" },
            PreferredLocations: new List<string> { "Nadiad" },
            MinimumDailyWage: 800,
            PreferredWorkingHours: "08:00 AM - 05:00 PM",
            FoodPreference: "Preferred",
            AccommodationPreference: "Required"
        ));

        // Verify Worker B preferences remained default/empty
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var userB = await db.Users.SingleAsync(u => u.Email == "worker.prefB@test.com");
        var profileB = await db.WorkerProfiles.SingleAsync(p => p.UserId == userB.Id);
        Assert.NotEqual(800, profileB.MinimumDailyWage);
        Assert.Null(profileB.PreferredWorkCategories);
    }

    [Fact]
    public async Task FarmerCannotModifyWorkerPreferences()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.noprefmod@test.com", "Password123!", "Farmer NoPref", Roles.Farmer);

        // Act
        var response = await farmerClient.PutAsJsonAsync("/api/worker/preferences", new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Hack" },
            PreferredLocations: new List<string> { "Hack" },
            MinimumDailyWage: 1000,
            PreferredWorkingHours: null,
            FoodPreference: null,
            AccommodationPreference: null
        ));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotModifyWorkerPreferences()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.noprefmod@test.com", "Password123!", "Customer NoPref", Roles.Customer);

        // Act
        var response = await customerClient.PutAsJsonAsync("/api/worker/preferences", new WorkerPreferencesUpdateRequest(
            PreferredWorkCategories: new List<string> { "Hack" },
            PreferredLocations: new List<string> { "Hack" },
            MinimumDailyWage: 1000,
            PreferredWorkingHours: null,
            FoodPreference: null,
            AccommodationPreference: null
        ));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedAccessRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/preferences");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
