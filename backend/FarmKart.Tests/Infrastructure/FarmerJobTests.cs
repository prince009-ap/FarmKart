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

public class FarmerJobTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerJobTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_FarmerJobTest_{Guid.NewGuid()}";
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
    public async Task GetJobs_AuthenticatedFarmer_WithZeroJobs_Returns200OK_WithEmptyList()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.zerojobs@test.com", "SecurePassword123!", "Zero Jobs Farmer", Roles.Farmer);

        // Act
        var response = await client.GetAsync("/api/farmer/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<FarmerJobResponse>>(_jsonOptions);
        Assert.NotNull(jobs);
        Assert.Empty(jobs);
    }

    [Fact]
    public async Task GetJobs_AuthenticatedFarmer_WithJobs_ReturnsOwnJobs()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.withjobs@test.com", "SecurePassword123!", "With Jobs Farmer", Roles.Farmer);

        var createJobRequest = new CreateFarmerJobRequest(
            Title: "Harvesting Help Required",
            Description: "Need experienced workers for wheat harvest",
            WorkCategory: "Harvesting",
            CropType: "Wheat",
            WorkersRequired: 3,
            RequiredExperience: 1,
            WagePerDay: 550,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            WorkingHours: "8 AM - 5 PM",
            FarmLocation: "Valley Farm",
            FarmSize: 10m,
            FoodProvided: true,
            AccommodationProvided: false,
            IsUrgent: true
        );

        var createResponse = await client.PostAsJsonAsync("/api/farmer/jobs", createJobRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Act
        var response = await client.GetAsync("/api/farmer/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<FarmerJobResponse>>(_jsonOptions);
        Assert.NotNull(jobs);
        Assert.Single(jobs);
        Assert.Equal("Harvesting Help Required", jobs[0].Title);
        Assert.Equal("Harvesting", jobs[0].WorkCategory);
        Assert.Equal(550, jobs[0].WagePerDay);
    }

    [Fact]
    public async Task GetJobs_Worker_Returns403Forbidden()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.jobget@test.com", "SecurePassword123!", "Worker Bob", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/farmer/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_Customer_Returns403Forbidden()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("customer.jobget@test.com", "SecurePassword123!", "Customer Alice", Roles.Customer);

        // Act
        var response = await client.GetAsync("/api/farmer/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_Unauthenticated_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/farmer/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_FarmerCannotSeeAnotherFarmersJobs()
    {
        // Arrange: Farmer 1 posts a job
        var farmer1Client = await GetAuthenticatedClientAsync("farmer1.isolation@test.com", "SecurePassword123!", "Farmer One", Roles.Farmer);
        var createJobRequest = new CreateFarmerJobRequest(
            Title: "Farmer 1 Special Job",
            Description: "Specialized job for farmer 1 only",
            WorkCategory: "Pruning",
            CropType: "Apples",
            WorkersRequired: 2,
            RequiredExperience: 2,
            WagePerDay: 700,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            WorkingHours: "7 AM - 4 PM",
            FarmLocation: "Orchard Hill",
            FarmSize: 5m,
            FoodProvided: false,
            AccommodationProvided: false,
            IsUrgent: false
        );
        var createResponse = await farmer1Client.PostAsJsonAsync("/api/farmer/jobs", createJobRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Arrange: Farmer 2 authenticates
        var farmer2Client = await GetAuthenticatedClientAsync("farmer2.isolation@test.com", "SecurePassword123!", "Farmer Two", Roles.Farmer);

        // Act: Farmer 2 requests jobs
        var response = await farmer2Client.GetAsync("/api/farmer/jobs");

        // Assert: Farmer 2 receives empty list and cannot see Farmer 1's job
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<FarmerJobResponse>>(_jsonOptions);
        Assert.NotNull(jobs);
        Assert.Empty(jobs);
    }

    [Fact]
    public async Task JobOwnership_IsDeterminedServerSide_FromAuthClaims()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("farmer.serverside@test.com", "SecurePassword123!", "Server Side Farmer", Roles.Farmer);
        var createJobRequest = new CreateFarmerJobRequest(
            Title: "Server Side Ownership Job",
            Description: "Verifying server-side ownership determination",
            WorkCategory: "Weeding",
            CropType: "Cotton",
            WorkersRequired: 4,
            RequiredExperience: 0,
            WagePerDay: 500,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            WorkingHours: "8 AM - 4 PM",
            FarmLocation: "Cotton Field",
            FarmSize: 12m,
            FoodProvided: true,
            AccommodationProvided: true,
            IsUrgent: false
        );

        // Act: Post job without passing any user or profile ID in body
        var createResponse = await client.PostAsJsonAsync("/api/farmer/jobs", createJobRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdJob = await createResponse.Content.ReadFromJsonAsync<FarmerJobResponse>(_jsonOptions);
        Assert.NotNull(createdJob);

        // Assert in DB that job belongs to Server Side Farmer's profile ID
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "farmer.serverside@test.com");
        var farmerProfile = await db.FarmerProfiles.SingleAsync(p => p.UserId == user.Id);
        var dbJob = await db.Jobs.SingleAsync(j => j.Id == createdJob.Id);

        Assert.Equal(farmerProfile.Id, dbJob.FarmerProfileId);
    }
}
