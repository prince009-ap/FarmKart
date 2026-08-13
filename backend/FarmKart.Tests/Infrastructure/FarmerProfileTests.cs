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

public class FarmerProfileTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerProfileTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_FarmerProfileTest_{Guid.NewGuid()}";
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
                fullName, email, password, "1234567890", null, "123 Farm Road", "Happy Farm", 10.5m, FarmSizeUnit.Vigha, "Near Valley"));
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
    public async Task GetProfile_Farmer_ReturnsOwnProfile()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.profile@test.com", "SecurePassword123!", "Farmer John", Roles.Farmer);

        // Act
        var response = await client.GetAsync("/api/farmer/profile");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<FarmerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("Farmer John", profile.FullName);
        Assert.Equal("farmer.profile@test.com", profile.Email);
        Assert.Equal("1234567890", profile.Phone);
        Assert.Equal("123 Farm Road", profile.Address);
        Assert.Equal("Happy Farm", profile.FarmName);
        Assert.Equal(10.5m, profile.FarmSize);
        Assert.Equal(FarmSizeUnit.Vigha, profile.FarmSizeUnit);
        Assert.Equal("Near Valley", profile.FarmLocation);
    }

    [Fact]
    public async Task UpdateProfile_Farmer_UpdatesOwnProfileCorrectly()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.update@test.com", "SecurePassword123!", "Farmer John", Roles.Farmer);
        var updateRequest = new FarmerProfileUpdateRequest(
            FullName: "John Doe Updated",
            Phone: "9876543210",
            Address: "456 New Road",
            FarmName: "Better Farm",
            FarmSize: 15.75m,
            FarmSizeUnit: FarmSizeUnit.Vigha,
            FarmLocation: "Far Valley"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/farmer/profile", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedProfile = await response.Content.ReadFromJsonAsync<FarmerProfileResponse>(_jsonOptions);
        Assert.NotNull(updatedProfile);
        Assert.Equal(updateRequest.FullName, updatedProfile.FullName);
        Assert.Equal(updateRequest.Phone, updatedProfile.Phone);
        Assert.Equal(updateRequest.Address, updatedProfile.Address);
        Assert.Equal(updateRequest.FarmName, updatedProfile.FarmName);
        Assert.Equal(updateRequest.FarmSize, updatedProfile.FarmSize);
        Assert.Equal(updateRequest.FarmSizeUnit, updatedProfile.FarmSizeUnit);
        Assert.Equal(updateRequest.FarmLocation, updatedProfile.FarmLocation);

        // Verify read-only field (email) didn't change
        Assert.Equal("farmer.update@test.com", updatedProfile.Email);
    }

    [Fact]
    public async Task GetProfile_Worker_Returns403Forbidden()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.profile@test.com", "SecurePassword123!", "Worker Bob", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/farmer/profile");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Customer_Returns403Forbidden()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("customer.profile@test.com", "SecurePassword123!", "Customer Alice", Roles.Customer);

        // Act
        var response = await client.GetAsync("/api/farmer/profile");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/farmer/profile");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_CannotUpdateAnotherUserProfile()
    {
        // Arrange
        var farmer1Client = await GetAuthenticatedClientAsync("farmer1.profile@test.com", "SecurePassword123!", "Farmer One", Roles.Farmer);
        
        // Register a second farmer
        await SetupTestUserAsync("farmer2.profile@test.com", "SecurePassword123!", "Farmer Two", Roles.Farmer);

        // Since the backend resolves UserId from claims/token, sending a PUT request to /api/farmer/profile
        // as farmer1 will only ever modify farmer1's profile. We can assert that farmer2's profile in the DB remains untouched.
        var updateRequest = new FarmerProfileUpdateRequest(
            FullName: "Attempted Modification",
            Phone: "0000000000",
            Address: "Hack Road",
            FarmName: "Hacked Farm",
            FarmSize: 100m,
            FarmSizeUnit: FarmSizeUnit.Vigha,
            FarmLocation: "Hack Location"
        );

        // Act
        var response = await farmer1Client.PutAsJsonAsync("/api/farmer/profile", updateRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert farmer2 profile remains unchanged
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var farmer2User = await db.Users.SingleAsync(u => u.Email == "farmer2.profile@test.com");
        var farmer2Profile = await db.FarmerProfiles.SingleAsync(p => p.UserId == farmer2User.Id);

        Assert.Equal("Farmer Two", farmer2Profile.FullName);
        Assert.Equal("123 Farm Road", farmer2Profile.AddressInfo.AddressLine);
    }

    [Fact]
    public async Task UpdateProfile_NegativeFarmSize_Returns400BadRequest()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.negative@test.com", "SecurePassword123!", "Farmer John", Roles.Farmer);
        var updateRequest = new FarmerProfileUpdateRequest(
            FullName: "John Doe",
            Phone: "1234567890",
            Address: "123 Farm Road",
            FarmName: "Happy Farm",
            FarmSize: -1.5m, // Negative size
            FarmSizeUnit: FarmSizeUnit.Vigha,
            FarmLocation: "Near Valley"
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/farmer/profile", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_InvalidFarmSizeUnit_Returns400BadRequest()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.invalidunit@test.com", "SecurePassword123!", "Farmer John", Roles.Farmer);
        
        // Send raw JSON to bypass DTO enum serialization constraints on client side if necessary,
        // or check how invalid unit values are handled.
        var jsonPayload = new
        {
            fullName = "John Doe",
            phone = "1234567890",
            address = "123 Farm Road",
            farmName = "Happy Farm",
            farmSize = 10.5,
            farmSizeUnit = "INVALID_UNIT" // Invalid Enum
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/farmer/profile", jsonPayload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_MissingFarmerProfile_Returns404NotFound()
    {
        // Arrange: Create a user with Farmer role but delete their FarmerProfile directly in DB
        var client = await GetAuthenticatedClientAsync("farmer.missing@test.com", "SecurePassword123!", "Farmer Joe", Roles.Farmer);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "farmer.missing@test.com");
            var profile = await db.FarmerProfiles.SingleAsync(p => p.UserId == user.Id);
            db.FarmerProfiles.Remove(profile);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/farmer/profile");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_PasswordHashAndCookieName_AreNotReturnedInResponseBody()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.secure@test.com", "SecurePassword123!", "Farmer John", Roles.Farmer);

        // Act
        var response = await client.GetAsync("/api/farmer/profile");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        
        Assert.DoesNotContain("Password", body);
        Assert.DoesNotContain("PasswordHash", body);
        Assert.DoesNotContain("SecurityStamp", body);
        Assert.DoesNotContain("Token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cookie", body, StringComparison.OrdinalIgnoreCase);
    }
}
