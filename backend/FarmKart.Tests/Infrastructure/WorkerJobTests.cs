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

public class WorkerJobTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerJobTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerJobTest_{Guid.NewGuid()}";
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

    private async Task<Guid> SeedOpenJobAsync(string title = "Harvesting Job")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var farmerEmail = $"farmer.{Guid.NewGuid()}@test.com";
        await SetupTestUserAsync(farmerEmail, "Password123!", "Farmer Seed", Roles.Farmer);

        var farmerUser = await db.Users.SingleAsync(u => u.Email == farmerEmail);
        var farmerProfile = await db.FarmerProfiles.SingleAsync(p => p.UserId == farmerUser.Id);

        var job = new Job
        {
            FarmerProfileId = farmerProfile.Id,
            Title = title,
            Description = "General harvesting work",
            WorkCategory = "Harvesting",
            CropType = "Rice",
            WorkersRequired = 5,
            RequiredExperience = 1,
            WagePerDay = 500,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            WorkingHours = "8 AM - 5 PM",
            FarmLocation = "Green Field",
            Status = JobStatus.Open
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public async Task WorkerCanRetrieveAvailableJobs()
    {
        // Arrange
        await SeedOpenJobAsync("Open Rice Harvesting Job");
        var client = await GetAuthenticatedClientAsync("worker.browse@test.com", "SecurePassword123!", "Worker Bob", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/worker/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<WorkerAvailableJobResponse>>(_jsonOptions);
        Assert.NotNull(jobs);
        Assert.NotEmpty(jobs);
        Assert.Contains(jobs, j => j.Title == "Open Rice Harvesting Job");
    }

    [Fact]
    public async Task WorkerReceivesEmptyListWhenNoJobsAvailable()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync("worker.emptyjobs@test.com", "SecurePassword123!", "Worker Sam", Roles.Worker);

        // Act
        var response = await client.GetAsync("/api/worker/jobs");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var jobs = await response.Content.ReadFromJsonAsync<List<WorkerAvailableJobResponse>>(_jsonOptions);
        Assert.NotNull(jobs);
        Assert.Empty(jobs);
    }

    [Fact]
    public async Task WorkerCanViewAvailableJobDetails()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("Detailed Job Title");
        var client = await GetAuthenticatedClientAsync("worker.details@test.com", "SecurePassword123!", "Worker Details", Roles.Worker);

        // Act
        var response = await client.GetAsync($"/api/worker/jobs/{jobId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<WorkerAvailableJobResponse>(_jsonOptions);
        Assert.NotNull(job);
        Assert.Equal(jobId, job.Id);
        Assert.Equal("Detailed Job Title", job.Title);
        Assert.False(job.HasApplied);
    }

    [Fact]
    public async Task WorkerCanApplyToAvailableJob()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("Applies Job");
        var client = await GetAuthenticatedClientAsync("worker.apply@test.com", "SecurePassword123!", "Worker Apply", Roles.Worker);
        var request = new ApplyJobRequest("Available for all 5 days.");

        // Act
        var response = await client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var application = await response.Content.ReadFromJsonAsync<WorkerJobApplicationResponse>(_jsonOptions);
        Assert.NotNull(application);
        Assert.Equal(jobId, application.JobId);
        Assert.Equal(ApplicationStatus.Pending, application.Status);
        Assert.Equal("Available for all 5 days.", application.Message);
    }

    [Fact]
    public async Task ApplicationBelongsToAuthenticatedWorker()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("Belongs Job");
        var client = await GetAuthenticatedClientAsync("worker.belongs@test.com", "SecurePassword123!", "Worker Belongs", Roles.Worker);

        // Act
        var response = await client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest(null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var application = await response.Content.ReadFromJsonAsync<WorkerJobApplicationResponse>(_jsonOptions);

        // Assert in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "worker.belongs@test.com");
        var workerProfile = await db.WorkerProfiles.SingleAsync(p => p.UserId == user.Id);
        var dbApp = await db.JobApplications.SingleAsync(a => a.Id == application!.ApplicationId);

        Assert.Equal(workerProfile.Id, dbApp.WorkerProfileId);
    }

    [Fact]
    public async Task WorkerCannotSubmitAnotherWorkerId()
    {
        // Arrange: The endpoint POST /api/worker/jobs/{id}/apply takes no WorkerId parameter in body.
        var jobId = await SeedOpenJobAsync("Server Ownership Job");
        var client = await GetAuthenticatedClientAsync("worker.serverown@test.com", "SecurePassword123!", "Server Ownership Worker", Roles.Worker);

        // Act
        var response = await client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest("Test Message"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert that the created application is owned by serverown worker only
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "worker.serverown@test.com");
        var workerProfile = await db.WorkerProfiles.SingleAsync(p => p.UserId == user.Id);
        var app = await db.JobApplications.SingleAsync(a => a.JobId == jobId);

        Assert.Equal(workerProfile.Id, app.WorkerProfileId);
    }

    [Fact]
    public async Task DuplicateApplicationIsRejected()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("Duplicate Job");
        var client = await GetAuthenticatedClientAsync("worker.duplicate@test.com", "SecurePassword123!", "Worker Duplicate", Roles.Worker);

        // Act 1: Apply first time
        var firstResponse = await client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest(null));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        // Act 2: Apply second time
        var secondResponse = await client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest(null));

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task UnavailableJobCannotBeAppliedTo()
    {
        // Arrange: Create a draft job (Status != Open)
        Guid draftJobId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerEmail = $"farmer.{Guid.NewGuid()}@test.com";
            await SetupTestUserAsync(farmerEmail, "Password123!", "Farmer Draft", Roles.Farmer);
            var farmerUser = await db.Users.SingleAsync(u => u.Email == farmerEmail);
            var farmerProfile = await db.FarmerProfiles.SingleAsync(p => p.UserId == farmerUser.Id);

            var draftJob = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Draft Job",
                Description = "Draft Description",
                WorkCategory = "Pruning",
                WorkersRequired = 2,
                RequiredExperience = 1,
                WagePerDay = 400,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                WorkingHours = "8 AM - 4 PM",
                FarmLocation = "Draft Location",
                Status = JobStatus.Draft
            };
            db.Jobs.Add(draftJob);
            await db.SaveChangesAsync();
            draftJobId = draftJob.Id;
        }

        var client = await GetAuthenticatedClientAsync("worker.draftapply@test.com", "SecurePassword123!", "Worker Draft", Roles.Worker);

        // Act
        var response = await client.PostAsJsonAsync($"/api/worker/jobs/{draftJobId}/apply", new ApplyJobRequest(null));

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotUseWorkerApplicationApis()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("Farmer Test Job");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.callworker@test.com", "SecurePassword123!", "Farmer Calling Worker", Roles.Farmer);

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest(null));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotUseWorkerApplicationApis()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("Customer Test Job");
        var customerClient = await GetAuthenticatedClientAsync("customer.callworker@test.com", "SecurePassword123!", "Customer Calling Worker", Roles.Customer);

        // Act
        var response = await customerClient.GetAsync("/api/worker/jobs");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUserReceives401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/jobs");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingWorkerProfileIsHandledCorrectly()
    {
        // Arrange: User with Worker role, but delete WorkerProfile in DB
        var client = await GetAuthenticatedClientAsync("worker.noprofile@test.com", "SecurePassword123!", "Worker No Profile", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "worker.noprofile@test.com");
            var profile = await db.WorkerProfiles.SingleAsync(p => p.UserId == user.Id);
            db.WorkerProfiles.Remove(profile);
            await db.SaveChangesAsync();
        }

        // Act: GET applications returns []
        var response = await client.GetAsync("/api/worker/applications");

        // Assert: 200 OK with empty array
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var apps = await response.Content.ReadFromJsonAsync<List<WorkerJobApplicationResponse>>(_jsonOptions);
        Assert.NotNull(apps);
        Assert.Empty(apps);
    }

    [Fact]
    public async Task WorkerCanRetrieveOwnApplications()
    {
        // Arrange
        var jobId = await SeedOpenJobAsync("App My Apps Job");
        var client = await GetAuthenticatedClientAsync("worker.myapps@test.com", "SecurePassword123!", "Worker My Apps", Roles.Worker);

        // Apply
        var applyRes = await client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest("Ready to work"));
        Assert.Equal(HttpStatusCode.OK, applyRes.StatusCode);

        // Act
        var response = await client.GetAsync("/api/worker/applications");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var apps = await response.Content.ReadFromJsonAsync<List<WorkerJobApplicationResponse>>(_jsonOptions);
        Assert.NotNull(apps);
        Assert.Single(apps);
        Assert.Equal(jobId, apps[0].JobId);
        Assert.Equal("App My Apps Job", apps[0].JobTitle);
        Assert.Equal(ApplicationStatus.Pending, apps[0].Status);
    }

    [Fact]
    public async Task WorkerCannotRetrieveAnotherWorkersApplications()
    {
        // Arrange: Worker 1 applies
        var jobId = await SeedOpenJobAsync("Isolation App Job");
        var worker1Client = await GetAuthenticatedClientAsync("worker1.appiso@test.com", "SecurePassword123!", "Worker One", Roles.Worker);
        await worker1Client.PostAsJsonAsync($"/api/worker/jobs/{jobId}/apply", new ApplyJobRequest(null));

        // Worker 2 authenticates
        var worker2Client = await GetAuthenticatedClientAsync("worker2.appiso@test.com", "SecurePassword123!", "Worker Two", Roles.Worker);

        // Act: Worker 2 gets applications
        var response = await worker2Client.GetAsync("/api/worker/applications");

        // Assert: Worker 2 gets empty list
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var apps = await response.Content.ReadFromJsonAsync<List<WorkerJobApplicationResponse>>(_jsonOptions);
        Assert.NotNull(apps);
        Assert.Empty(apps);
    }
}
