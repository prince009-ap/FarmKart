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

public class WorkerWorkHistoryTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerWorkHistoryTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerHistoryTest_{Guid.NewGuid()}";
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
    public async Task WorkerCanRetrieveOwnCompletedWorkHistory()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.historyget@test.com", "Password123!", "Worker HistoryGet", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync("/api/worker/work-history");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.NotNull(summary.HistoryItems);
    }

    [Fact]
    public async Task CompletedAssignmentsAppearInHistory()
    {
        // Arrange
        await SetupTestUserAsync("farmer.histcomp@test.com", "Password123!", "Farmer HistComp", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.histcomp@test.com", "Password123!", "Worker HistComp", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.histcomp@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.histcomp@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
            var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)); // 6 days

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Wheat Harvesting 6 Days",
                Description = "Harvesting",
                WorkCategory = "Harvesting",
                WorkersRequired = 2,
                WagePerDay = 600,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = JobStatus.Completed
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = AssignmentStatus.Completed
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/work-history");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalCompletedJobs);
        Assert.Single(summary.HistoryItems);
        Assert.Equal("Wheat Harvesting 6 Days", summary.HistoryItems[0].JobTitle);
    }

    [Fact]
    public async Task IncompleteAssignmentsDoNotAppearAsCompletedHistory()
    {
        // Arrange
        await SetupTestUserAsync("farmer.histincomp@test.com", "Password123!", "Farmer HistIncomp", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.histincomp@test.com", "Password123!", "Worker HistIncomp", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.histincomp@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.histincomp@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var futureStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Future Pending Job",
                Description = "Future",
                WorkCategory = "Sowing",
                WorkersRequired = 2,
                WagePerDay = 500,
                StartDate = futureStart,
                EndDate = futureEnd,
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = futureStart,
                EndDate = futureEnd,
                Status = AssignmentStatus.Pending
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/work-history");

        // Assert: 0 history items for open pending future assignment
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalCompletedJobs);
        Assert.Empty(summary.HistoryItems);
    }

    [Fact]
    public async Task JobInformationIsReturnedCorrectly()
    {
        // Arrange
        await SetupTestUserAsync("farmer.jobinfo@test.com", "Password123!", "Farmer JobInfo", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.jobinfo@test.com", "Password123!", "Worker JobInfo", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.jobinfo@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.jobinfo@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
            var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Paddy Transplanting",
                Description = "Paddy work",
                WorkCategory = "Transplanting",
                WorkersRequired = 1,
                WagePerDay = 550,
                StartDate = pastStart,
                EndDate = pastEnd,
                FarmLocation = "Surat Farm",
                Status = JobStatus.Completed
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = AssignmentStatus.Completed
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/work-history");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Single(summary.HistoryItems);
        var item = summary.HistoryItems[0];
        Assert.Equal("Paddy Transplanting", item.JobTitle);
        Assert.Equal("Transplanting", item.WorkCategory);
        Assert.Equal("Surat Farm", item.Location);
        Assert.Equal(550, item.DailyWage);
    }

    [Fact]
    public async Task AttendanceSummaryIsReturnedCorrectly()
    {
        // Arrange
        await SetupTestUserAsync("farmer.attsum@test.com", "Password123!", "Farmer AttSum", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.attsum@test.com", "Password123!", "Worker AttSum", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.attsum@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.attsum@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
            var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Att Summary Job",
                Description = "Att Job",
                WorkCategory = "Maintenance",
                WorkersRequired = 1,
                WagePerDay = 500,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = JobStatus.Completed
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = AssignmentStatus.Completed
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            db.Attendances.Add(new Attendance { WorkerAssignmentId = assignment.Id, Date = pastStart, Status = AttendanceStatus.Present });
            db.Attendances.Add(new Attendance { WorkerAssignmentId = assignment.Id, Date = pastStart.AddDays(1), Status = AttendanceStatus.Present });
            db.Attendances.Add(new Attendance { WorkerAssignmentId = assignment.Id, Date = pastStart.AddDays(2), Status = AttendanceStatus.HalfDay });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/work-history");

        // Assert: 2 Present, 1 HalfDay -> 2.5 days worked
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Single(summary.HistoryItems);
        var item = summary.HistoryItems[0];
        Assert.Equal(2, item.PresentCount);
        Assert.Equal(1, item.HalfDayCount);
        Assert.Equal(2.5m, item.DaysWorked);
    }

    [Fact]
    public async Task EarningsAreConsistentWithPhase57()
    {
        // Arrange
        await SetupTestUserAsync("farmer.histearn@test.com", "Password123!", "Farmer HistEarn", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.histearn@test.com", "Password123!", "Worker HistEarn", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.histearn@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.histearn@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
            var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)); // 6 days

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Wheat Harvesting 6 Days",
                Description = "Harvesting",
                WorkCategory = "Harvesting",
                WorkersRequired = 2,
                WagePerDay = 600,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = JobStatus.Completed
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = AssignmentStatus.Completed
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();
        }

        // Act 1: Get Earnings endpoint
        var earnResp = await workerClient.GetAsync("/api/worker/earnings");
        var earnSummary = await earnResp.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);

        // Act 2: Get Work History endpoint
        var histResp = await workerClient.GetAsync("/api/worker/work-history");
        var histSummary = await histResp.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);

        // Assert: Total Earnings match exactly
        Assert.NotNull(earnSummary);
        Assert.NotNull(histSummary);
        Assert.Equal(earnSummary.TotalEarnings, histSummary.TotalEarnings);
    }

    [Fact]
    public async Task RatingIsShownWhenAvailable()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.histrate@test.com", "Password123!", "Farmer HistRate", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.histrate@test.com", "Password123!", "Worker HistRate", Roles.Worker);

        Guid assignmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.histrate@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.histrate@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
            var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5));

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Rated Job",
                Description = "Rated",
                WorkCategory = "Sowing",
                WorkersRequired = 1,
                WagePerDay = 500,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = JobStatus.Completed
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = AssignmentStatus.Completed
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            assignmentId = assignment.Id;
        }

        // Farmer rates worker
        await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{assignmentId}/review", new CreateWorkerReviewRequest(Rating: 5, Comment: "Exceptional worker"));

        // Act: Worker gets history
        var response = await workerClient.GetAsync("/api/worker/work-history");

        // Assert: Rating & comment are populated
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Single(summary.HistoryItems);
        Assert.Equal(5, summary.HistoryItems[0].Rating);
        Assert.Equal("Exceptional worker", summary.HistoryItems[0].ReviewComment);
    }

    [Fact]
    public async Task WorkerCannotAccessAnotherWorkerHistory()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.histA@test.com", "Password123!", "Worker A", Roles.Worker);
        await SetupTestUserAsync("worker.histB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Act: Worker A gets own history
        var response = await workerAClient.GetAsync("/api/worker/work-history");

        // Assert: Returns empty history for Worker A
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerWorkHistorySummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalCompletedJobs);
        Assert.Empty(summary.HistoryItems);
    }

    [Fact]
    public async Task WorkerCannotModifyHistory()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.histnomod@test.com", "Password123!", "Worker NoMod", Roles.Worker);

        // Act: Work history endpoint is GET only
        var response = await workerClient.PostAsJsonAsync("/api/worker/work-history", new { });

        // Assert: 405 Method Not Allowed or 404
        Assert.True(response.StatusCode == HttpStatusCode.MethodNotAllowed || response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FarmerCannotAccessPrivateWorkerHistoryEndpoint()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.nohist@test.com", "Password123!", "Farmer NoHist", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync("/api/worker/work-history");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotAccessPrivateWorkerHistoryEndpoint()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.nohist@test.com", "Password123!", "Customer NoHist", Roles.Customer);

        // Act
        var response = await customerClient.GetAsync("/api/worker/work-history");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedAccessIsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/work-history");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
