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

public class WorkerProfileTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerProfileTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerProfileTest_{Guid.NewGuid()}";
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
    public async Task AuthenticatedWorkerCanGetOwnProfile()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.getprofile@test.com", "Password123!", "Worker Get", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/worker/profile");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("Worker Get", profile.FullName);
        Assert.Equal("worker.getprofile@test.com", profile.Email);
        Assert.Equal("9876543210", profile.Phone);
        Assert.Equal("123 Worker Road", profile.Address);
        Assert.Equal(2, profile.ExperienceYears);
        Assert.Equal(100, profile.ExpectedDailyWage);
    }

    [Fact]
    public async Task UnauthenticatedUserCannotGetWorkerProfile()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/profile");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotAccessWorkerProfile()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.noworkerprof@test.com", "Password123!", "Farmer NoAccess", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync("/api/worker/profile");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotAccessWorkerProfile()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.noworkerprof@test.com", "Password123!", "Customer NoAccess", Roles.Customer);

        // Act
        var response = await customerClient.GetAsync("/api/worker/profile");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCanUpdateOwnProfile()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.updateprof@test.com", "Password123!", "Worker Update", Roles.Worker);
        var updateReq = new WorkerProfileUpdateRequest(
            FullName: "Updated Worker Name",
            Phone: "9998887776",
            Address: "456 New Worker St",
            ExperienceYears: 5,
            ExpectedDailyWage: 250,
            ProfileImageUrl: "https://example.com/photo.jpg",
            IsAvailable: true,
            AvailableFrom: null,
            AvailabilityNotes: "Ready for work"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", updateReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Updated Worker Name", updated.FullName);
        Assert.Equal("9998887776", updated.Phone);
        Assert.Equal("456 New Worker St", updated.Address);
        Assert.Equal(5, updated.ExperienceYears);
        Assert.Equal(250, updated.ExpectedDailyWage);
        Assert.Equal("https://example.com/photo.jpg", updated.ProfileImageUrl);
        Assert.Equal("worker.updateprof@test.com", updated.Email);
    }

    [Fact]
    public async Task WorkerCannotUpdateAnotherUsersProfile()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.A@test.com", "Password123!", "Worker A", Roles.Worker);

        // Create Worker B
        await SetupTestUserAsync("worker.B@test.com", "Password123!", "Worker B", Roles.Worker);

        // Worker A updates profile
        var updateReq = new WorkerProfileUpdateRequest("Hacked Name", "9990001112", "Hacked Address", 10, 500);
        var response = await workerAClient.PutAsJsonAsync("/api/worker/profile", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify Worker B profile was NOT modified
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var userB = await db.Users.SingleAsync(u => u.Email == "worker.B@test.com");
        var profileB = await db.WorkerProfiles.SingleAsync(p => p.UserId == userB.Id);
        Assert.Equal("Worker B", profileB.FullName);
        Assert.NotEqual("Hacked Name", profileB.FullName);
    }

    [Fact]
    public async Task EmailCannotBeChangedThroughProfileUpdate()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.emailimmutable@test.com", "Password123!", "Worker Immutable", Roles.Worker);
        var updateReq = new WorkerProfileUpdateRequest("Worker Immutable", "9876543210", "123 Address", 2, 100);

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", updateReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("worker.emailimmutable@test.com", profile.Email);
    }

    [Fact]
    public async Task InvalidPhoneNumberIsRejected()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.invalidphone@test.com", "Password123!", "Worker InvPhone", Roles.Worker);
        var updateReq = new WorkerProfileUpdateRequest("Worker InvPhone", "invalid-phone!", "123 Address", 2, 100);

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", updateReq);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NegativeExperienceIsRejected()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.negexp@test.com", "Password123!", "Worker NegExp", Roles.Worker);
        var updateReq = new WorkerProfileUpdateRequest("Worker NegExp", "9876543210", "123 Address", -1, 100);

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", updateReq);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PasswordOrHashIsNeverReturnedInProfileResponse()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.secureuser@test.com", "Password123!", "Worker SecureUser", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/worker/profile");
        var rawJson = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", rawJson, StringComparison.OrdinalIgnoreCase);
    }
}
