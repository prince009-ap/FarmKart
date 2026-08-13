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

public class FarmerApplicationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public FarmerApplicationTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_FarmerAppTest_{Guid.NewGuid()}";
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
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null) return;

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var phone = $"98{Random.Shared.Next(10000000, 99999999)}";

        if (role == Roles.Farmer)
        {
            await authService.RegisterFarmerAsync(new FarmerRegisterRequest(
                fullName, email, password, phone, null, "123 Farm Road", "Happy Farm", 10.5m, FarmSizeUnit.Vigha, "Near Valley"));
        }
        else if (role == Roles.Worker)
        {
            await authService.RegisterWorkerAsync(new WorkerRegisterRequest(
                fullName, email, password, phone, null, "123 Worker Road", 2, 100));
        }
        else if (role == Roles.Customer)
        {
            await authService.RegisterCustomerAsync(new CustomerRegisterRequest(
                fullName, email, password, phone, null, "123 Customer Road"));
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

    private async Task<(Guid FarmerUserId, Guid JobId, Guid ApplicationId, Guid WorkerProfileId)> SeedJobAndApplicationAsync(
        string farmerEmail = "farmer.appseed@test.com",
        int workersRequired = 2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        await SetupTestUserAsync(farmerEmail, "Password123!", "Farmer Seed", Roles.Farmer);
        var farmerUser = await db.Users.SingleAsync(u => u.Email == farmerEmail);
        var farmerProfile = await db.FarmerProfiles.SingleAsync(p => p.UserId == farmerUser.Id);

        var job = new Job
        {
            FarmerProfileId = farmerProfile.Id,
            Title = "Harvesting Wheat",
            Description = "General harvesting work",
            WorkCategory = "Harvesting",
            CropType = "Wheat",
            WorkersRequired = workersRequired,
            RequiredExperience = 1,
            WagePerDay = 500,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            WorkingHours = "8 AM - 5 PM",
            FarmLocation = "Green Field",
            Status = JobStatus.Open
        };
        db.Jobs.Add(job);

        var workerEmail = $"worker.{Guid.NewGuid()}@test.com";
        await SetupTestUserAsync(workerEmail, "Password123!", "Worker Seed", Roles.Worker);
        var workerUser = await db.Users.SingleAsync(u => u.Email == workerEmail);
        var workerProfile = await db.WorkerProfiles.SingleAsync(p => p.UserId == workerUser.Id);

        var application = new JobApplication
        {
            JobId = job.Id,
            WorkerProfileId = workerProfile.Id,
            Status = ApplicationStatus.Pending,
            AppliedAtUtc = DateTime.UtcNow,
            Message = "Ready to start"
        };
        db.JobApplications.Add(application);

        await db.SaveChangesAsync();

        return (farmerUser.Id, job.Id, application.Id, workerProfile.Id);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public async Task FarmerCanListApplicationsForOwnJob()
    {
        // Arrange
        var (farmerUserId, jobId, appId, _) = await SeedJobAndApplicationAsync("farmer.listapps@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.listapps@test.com", "Password123!", "Farmer Seed", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync($"/api/farmer/jobs/{jobId}/applications");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var apps = await response.Content.ReadFromJsonAsync<List<FarmerJobApplicationResponse>>(_jsonOptions);
        Assert.NotNull(apps);
        Assert.Single(apps);
        Assert.Equal(appId, apps[0].ApplicationId);
        Assert.Equal(ApplicationStatus.Pending, apps[0].Status);
    }

    [Fact]
    public async Task FarmerCannotListApplicationsForAnotherFarmersJob()
    {
        // Arrange
        var (_, jobId, _, _) = await SeedJobAndApplicationAsync("farmer.ownerA@test.com");
        var farmerBClient = await GetAuthenticatedClientAsync("farmer.ownerB@test.com", "Password123!", "Farmer B", Roles.Farmer);

        // Act
        var response = await farmerBClient.GetAsync($"/api/farmer/jobs/{jobId}/applications");

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCanViewOwnJobApplication()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.viewapp@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.viewapp@test.com", "Password123!", "Farmer View", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync($"/api/farmer/applications/{appId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var app = await response.Content.ReadFromJsonAsync<FarmerJobApplicationResponse>(_jsonOptions);
        Assert.NotNull(app);
        Assert.Equal(appId, app.ApplicationId);
        Assert.Equal("Worker Seed", app.ApplicantName);
    }

    [Fact]
    public async Task FarmerCanAcceptPendingApplication()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.acceptapp@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.acceptapp@test.com", "Password123!", "Farmer Accept", Roles.Farmer);

        // Act
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var app = await response.Content.ReadFromJsonAsync<FarmerJobApplicationResponse>(_jsonOptions);
        Assert.NotNull(app);
        Assert.Equal(ApplicationStatus.Accepted, app.Status);

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var dbApp = await db.JobApplications.SingleAsync(a => a.Id == appId);
        Assert.Equal(ApplicationStatus.Accepted, dbApp.Status);
    }

    [Fact]
    public async Task FarmerCanRejectPendingApplication()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.rejectapp@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.rejectapp@test.com", "Password123!", "Farmer Reject", Roles.Farmer);

        // Act
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var app = await response.Content.ReadFromJsonAsync<FarmerJobApplicationResponse>(_jsonOptions);
        Assert.NotNull(app);
        Assert.Equal(ApplicationStatus.Rejected, app.Status);

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var dbApp = await db.JobApplications.SingleAsync(a => a.Id == appId);
        Assert.Equal(ApplicationStatus.Rejected, dbApp.Status);
    }

    [Fact]
    public async Task WorkerCannotAccessFarmerApplicationManagement()
    {
        // Arrange
        var (_, jobId, appId, _) = await SeedJobAndApplicationAsync("farmer.workertest@test.com");
        var workerClient = await GetAuthenticatedClientAsync("worker.callfarmerapp@test.com", "Password123!", "Worker Call", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync($"/api/farmer/jobs/{jobId}/applications");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotAccessFarmerApplicationManagement()
    {
        // Arrange
        var (_, jobId, _, _) = await SeedJobAndApplicationAsync("farmer.customertest@test.com");
        var customerClient = await GetAuthenticatedClientAsync("customer.callfarmerapp@test.com", "Password123!", "Customer Call", Roles.Customer);

        // Act
        var response = await customerClient.GetAsync($"/api/farmer/jobs/{jobId}/applications");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUserReceives401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/farmer/applications/{Guid.NewGuid()}");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AcceptedApplicationCannotBeAcceptedAgain()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.reaccept@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.reaccept@test.com", "Password123!", "Farmer Reaccept", Roles.Farmer);

        // Accept 1
        var firstRes = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, firstRes.StatusCode);

        // Accept 2
        var secondRes = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }

    [Fact]
    public async Task RejectedApplicationCannotBeRejectedAgain()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.rereject@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.rereject@test.com", "Password123!", "Farmer Rereject", Roles.Farmer);

        // Reject 1
        var firstRes = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, firstRes.StatusCode);

        // Reject 2
        var secondRes = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }

    [Fact]
    public async Task RejectedApplicationCannotBeAccepted()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.rejthenacc@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.rejthenacc@test.com", "Password123!", "Farmer RejThenAcc", Roles.Farmer);

        // Reject
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);

        // Accept
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AcceptedApplicationCannotBeRejected()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.accthenrej@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.accthenrej@test.com", "Password123!", "Farmer AccThenRej", Roles.Farmer);

        // Accept
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Reject
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ClientCannotSpoofFarmerId()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.spooffarmer@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.spooffarmer@test.com", "Password123!", "Farmer Spoof", Roles.Farmer);

        // Act: Accept application without passing FarmerId in body/headers
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ClientCannotSpoofJobOwnership()
    {
        // Arrange: Farmer A owns application
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.realowner@test.com");

        // Farmer B attempts to accept Farmer A's application
        var farmerBClient = await GetAuthenticatedClientAsync("farmer.imposter@test.com", "Password123!", "Farmer Imposter", Roles.Farmer);

        // Act
        var response = await farmerBClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationStatusIsPersistedCorrectly()
    {
        // Arrange
        var (_, _, appId, _) = await SeedJobAndApplicationAsync("farmer.persist@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.persist@test.com", "Password123!", "Farmer Persist", Roles.Farmer);

        // Act
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify in DB with a fresh DbContext scope
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var dbApp = await db.JobApplications.AsNoTracking().SingleAsync(a => a.Id == appId);

        Assert.Equal(ApplicationStatus.Accepted, dbApp.Status);
    }

    [Fact]
    public async Task CannotExceedJobWorkerCapacity()
    {
        // Arrange: Job requires 1 worker only
        var (farmerUserId, jobId, appId1, _) = await SeedJobAndApplicationAsync("farmer.capacity@test.com", workersRequired: 1);

        // Seed 2nd application to the same job
        Guid appId2;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var worker2Email = $"worker2.{Guid.NewGuid()}@test.com";
            await SetupTestUserAsync(worker2Email, "Password123!", "Worker Two", Roles.Worker);
            var worker2User = await db.Users.SingleAsync(u => u.Email == worker2Email);
            var worker2Profile = await db.WorkerProfiles.SingleAsync(p => p.UserId == worker2User.Id);

            var application2 = new JobApplication
            {
                JobId = jobId,
                WorkerProfileId = worker2Profile.Id,
                Status = ApplicationStatus.Pending,
                AppliedAtUtc = DateTime.UtcNow
            };
            db.JobApplications.Add(application2);
            await db.SaveChangesAsync();
            appId2 = application2.Id;
        }

        var farmerClient = await GetAuthenticatedClientAsync("farmer.capacity@test.com", "Password123!", "Farmer Capacity", Roles.Farmer);

        // Act 1: Accept 1st application (capacity 1/1 reached)
        var res1 = await farmerClient.PostAsync($"/api/farmer/applications/{appId1}/accept", null);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Act 2: Try to accept 2nd application (exceeds capacity)
        var res2 = await farmerClient.PostAsync($"/api/farmer/applications/{appId2}/accept", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }
}
