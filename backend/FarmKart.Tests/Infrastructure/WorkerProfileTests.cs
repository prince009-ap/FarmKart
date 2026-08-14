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
    public async Task WorkerCanRetrieveOwnSkills()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.getskills@test.com", "Password123!", "Worker Skills", Roles.Worker);

        // Update skills first
        var updateReq = new WorkerProfileUpdateRequest(
            FullName: "Worker Skills",
            Phone: "9876543210",
            Address: "123 Worker Road",
            ExperienceYears: 3,
            Skills: new List<string> { "Harvesting", "Irrigation" }
        );
        await client.PutAsJsonAsync("/api/worker/profile", updateReq);

        // Act
        var response = await client.GetAsync("/api/worker/profile");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile);
        Assert.NotNull(profile.Skills);
        Assert.Contains("Harvesting", profile.Skills);
        Assert.Contains("Irrigation", profile.Skills);
    }

    [Fact]
    public async Task WorkerCanUpdateOwnSkills()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.updateskills@test.com", "Password123!", "Worker UpSkills", Roles.Worker);
        var updateReq = new WorkerProfileUpdateRequest(
            FullName: "Worker UpSkills",
            Phone: "9876543210",
            Address: "123 Worker Road",
            ExperienceYears: 4,
            Skills: new List<string> { "Sowing", "Tractor Operation" }
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", updateReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile?.Skills);
        Assert.Equal(2, profile.Skills.Count);
        Assert.Contains("Sowing", profile.Skills);
        Assert.Contains("Tractor Operation", profile.Skills);
    }

    [Fact]
    public async Task WorkerCanAddSkills()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.addskills@test.com", "Password123!", "Worker AddSkills", Roles.Worker);

        // Step 1: Set initial skill
        await client.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Worker AddSkills", "9876543210", "123 Road", 2, 100, Skills: new List<string> { "Harvesting" }));

        // Step 2: Add second skill
        var response = await client.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Worker AddSkills", "9876543210", "123 Road", 2, 100, Skills: new List<string> { "Harvesting", "Crop Maintenance" }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile?.Skills);
        Assert.Equal(2, profile.Skills.Count);
        Assert.Contains("Harvesting", profile.Skills);
        Assert.Contains("Crop Maintenance", profile.Skills);
    }

    [Fact]
    public async Task WorkerCanRemoveSkills()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.remskills@test.com", "Password123!", "Worker RemSkills", Roles.Worker);

        // Step 1: Set initial skills
        await client.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Worker RemSkills", "9876543210", "123 Road", 2, 100, Skills: new List<string> { "Harvesting", "Sowing", "Irrigation" }));

        // Step 2: Remove "Sowing"
        var response = await client.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Worker RemSkills", "9876543210", "123 Road", 2, 100, Skills: new List<string> { "Harvesting", "Irrigation" }));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile?.Skills);
        Assert.Equal(2, profile.Skills.Count);
        Assert.DoesNotContain("Sowing", profile.Skills);
    }

    [Fact]
    public async Task DuplicateSkillsAreHandledCorrectly()
    {
        // Arrange: Pass duplicate skill names ("Harvesting", "harvesting", "HARVESTING")
        var client = await GetAuthenticatedClientAsync("worker.dupskills@test.com", "Password123!", "Worker DupSkills", Roles.Worker);
        var req = new WorkerProfileUpdateRequest("Worker DupSkills", "9876543210", "123 Road", 2, 100, Skills: new List<string> { "Harvesting", "harvesting", "HARVESTING", "Sowing" });

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", req);

        // Assert: Deduplicated to 2 skills
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile?.Skills);
        Assert.Equal(2, profile.Skills.Count);
    }

    [Fact]
    public async Task WorkerCanUpdateYearsOfExperience()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.exp@test.com", "Password123!", "Worker Exp", Roles.Worker);
        var req = new WorkerProfileUpdateRequest("Worker Exp", "9876543210", "123 Road", 8, 200);

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile);
        Assert.Equal(8, profile.ExperienceYears);
    }

    [Fact]
    public async Task WorkerCanUpdateExperienceDescription()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.expdesc@test.com", "Password123!", "Worker ExpDesc", Roles.Worker);
        var req = new WorkerProfileUpdateRequest(
            FullName: "Worker ExpDesc",
            Phone: "9876543210",
            Address: "123 Road",
            ExperienceYears: 4,
            ExperienceDescription: "Worked on wheat and cotton harvesting and basic irrigation activities."
        );

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<WorkerProfileResponse>(_jsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("Worked on wheat and cotton harvesting and basic irrigation activities.", profile.ExperienceDescription);
    }

    [Fact]
    public async Task NegativeExperienceIsRejected()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.negexp2@test.com", "Password123!", "Worker NegExp", Roles.Worker);
        var req = new WorkerProfileUpdateRequest("Worker NegExp", "9876543210", "123 Road", -2, 100);

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptySkillIsRejected()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.emptyskill@test.com", "Password123!", "Worker EmptySkill", Roles.Worker);
        var req = new WorkerProfileUpdateRequest("Worker EmptySkill", "9876543210", "123 Road", 2, 100, Skills: new List<string> { "   " });

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUserIsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Anon", "9876543210", "123 Road", 1, 50));

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotUpdateWorkerSkills()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.noskillupdate@test.com", "Password123!", "Farmer NoSkill", Roles.Farmer);

        // Act
        var response = await farmerClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Hack", "9876543210", "123 Road", 5, 100, Skills: new List<string> { "Hacked" }));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotUpdateWorkerSkills()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.noskillupdate@test.com", "Password123!", "Customer NoSkill", Roles.Customer);

        // Act
        var response = await customerClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Hack", "9876543210", "123 Road", 5, 100, Skills: new List<string> { "Hacked" }));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotModifyAnotherWorkersProfile()
    {
        // Arrange: Worker A
        var workerAClient = await GetAuthenticatedClientAsync("worker.skillA@test.com", "Password123!", "Worker A", Roles.Worker);

        // Worker B
        await SetupTestUserAsync("worker.skillB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Worker A updates profile
        await workerAClient.PutAsJsonAsync("/api/worker/profile", new WorkerProfileUpdateRequest("Worker A Mod", "9876543210", "123 Road", 5, 100, Skills: new List<string> { "Harvesting" }));

        // Verify Worker B profile was untouched
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var userB = await db.Users.SingleAsync(u => u.Email == "worker.skillB@test.com");
        var profileB = await db.WorkerProfiles.Include(p => p.WorkerSkills).SingleAsync(p => p.UserId == userB.Id);
        Assert.Equal("Worker B", profileB.FullName);
        Assert.Empty(profileB.WorkerSkills);
    }

    [Fact]
    public async Task PasswordOrHashIsNeverReturned()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.secure@test.com", "Password123!", "Worker Secure", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/worker/profile");
        var rawJson = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain("password", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", rawJson, StringComparison.OrdinalIgnoreCase);
    }
}
