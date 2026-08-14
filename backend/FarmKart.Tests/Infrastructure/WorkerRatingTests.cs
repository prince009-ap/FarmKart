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

public class WorkerRatingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerRatingTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerRatingTest_{Guid.NewGuid()}";
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

    private async Task<(Guid JobId, Guid AssignmentId, Guid WorkerUserId)> SetupCompletedAssignmentAsync(string farmerEmail, string workerEmail)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var farmerUser = await db.Users.SingleAsync(u => u.Email == farmerEmail);
        var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

        var workerUser = await db.Users.SingleAsync(u => u.Email == workerEmail);
        var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        var pastEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));

        var job = new Job
        {
            FarmerProfileId = farmerProfile.Id,
            Title = "Harvesting Wheat Completed",
            Description = "Completed harvesting work",
            WorkCategory = "Harvesting",
            WorkersRequired = 2,
            WagePerDay = 500,
            StartDate = pastDate,
            EndDate = pastEndDate,
            Status = JobStatus.Completed
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var assignment = new WorkerAssignment
        {
            JobId = job.Id,
            WorkerProfileId = workerProfile.Id,
            StartDate = pastDate,
            EndDate = pastEndDate,
            Status = AssignmentStatus.Completed
        };
        db.WorkerAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return (job.Id, assignment.Id, workerUser.Id);
    }

    [Fact]
    public async Task FarmerCanRateWorkerAfterCompletedWork()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.rate@test.com", "Password123!", "Farmer Rate", Roles.Farmer);
        await SetupTestUserAsync("worker.rate@test.com", "Password123!", "Worker Rate", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.rate@test.com", "worker.rate@test.com");

        // Act
        var req = new CreateWorkerReviewRequest(Rating: 5, Comment: "Very hardworking and completed work on time.");
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = await response.Content.ReadFromJsonAsync<WorkerReviewResponse>(_jsonOptions);
        Assert.NotNull(review);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Very hardworking and completed work on time.", review.Comment);
    }

    [Fact]
    public async Task Rating1To5Accepted()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.ratingval@test.com", "Password123!", "Farmer RatingVal", Roles.Farmer);
        await SetupTestUserAsync("worker.ratingval@test.com", "Password123!", "Worker RatingVal", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.ratingval@test.com", "worker.ratingval@test.com");

        // Act & Assert for 1 star
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 1, Comment: "Fair"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RatingBelow1Rejected()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.lowrating@test.com", "Password123!", "Farmer LowRating", Roles.Farmer);
        await SetupTestUserAsync("worker.lowrating@test.com", "Password123!", "Worker LowRating", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.lowrating@test.com", "worker.lowrating@test.com");

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 0, Comment: "Invalid"));

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RatingAbove5Rejected()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.highrating@test.com", "Password123!", "Farmer HighRating", Roles.Farmer);
        await SetupTestUserAsync("worker.highrating@test.com", "Password123!", "Worker HighRating", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.highrating@test.com", "worker.highrating@test.com");

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 6, Comment: "Invalid"));

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReviewTextIsOptional()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.optcomment@test.com", "Password123!", "Farmer OptComment", Roles.Farmer);
        await SetupTestUserAsync("worker.optcomment@test.com", "Password123!", "Worker OptComment", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.optcomment@test.com", "worker.optcomment@test.com");

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 4, Comment: null));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var review = await response.Content.ReadFromJsonAsync<WorkerReviewResponse>(_jsonOptions);
        Assert.NotNull(review);
        Assert.Equal(4, review.Rating);
        Assert.Null(review.Comment);
    }

    [Fact]
    public async Task ReviewLengthValidationWorks()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.longcomment@test.com", "Password123!", "Farmer LongComment", Roles.Farmer);
        await SetupTestUserAsync("worker.longcomment@test.com", "Password123!", "Worker LongComment", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.longcomment@test.com", "worker.longcomment@test.com");

        var longComment = new string('A', 2001);

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 5, Comment: longComment));

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotRateUnrelatedWorker()
    {
        // Arrange: Farmer A, Farmer B, Worker
        var farmerAClient = await GetAuthenticatedClientAsync("farmer.unrelatedA@test.com", "Password123!", "Farmer A", Roles.Farmer);
        await SetupTestUserAsync("farmer.unrelatedB@test.com", "Password123!", "Farmer B", Roles.Farmer);
        await SetupTestUserAsync("worker.unrelated@test.com", "Password123!", "Worker Unrelated", Roles.Worker);

        // Assignment belongs to Farmer B
        var setup = await SetupCompletedAssignmentAsync("farmer.unrelatedB@test.com", "worker.unrelated@test.com");

        // Act: Farmer A tries to rate Farmer B's worker assignment
        var response = await farmerAClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 5, Comment: "Hack"));

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotRateWorker()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.norate@test.com", "Password123!", "Customer NoRate", Roles.Customer);

        // Act
        var response = await customerClient.PostAsJsonAsync($"/api/farmer/assignments/{Guid.NewGuid()}/review", new CreateWorkerReviewRequest(Rating: 5));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotRateThemselves()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.norate@test.com", "Password123!", "Worker NoRate", Roles.Worker);

        // Act
        var response = await workerClient.PostAsJsonAsync($"/api/farmer/assignments/{Guid.NewGuid()}/review", new CreateWorkerReviewRequest(Rating: 5));

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateRatingForSameAssignmentPrevented()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.duprating@test.com", "Password123!", "Farmer DupRating", Roles.Farmer);
        await SetupTestUserAsync("worker.duprating@test.com", "Password123!", "Worker DupRating", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.duprating@test.com", "worker.duprating@test.com");

        // First rating: 4 stars
        var resp1 = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 4, Comment: "Good"));
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // Second rating: updates to 5 stars
        var resp2 = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 5, Comment: "Excellent"));
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var updated = await resp2.Content.ReadFromJsonAsync<WorkerReviewResponse>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(5, updated.Rating);
        Assert.Equal("Excellent", updated.Comment);
    }

    [Fact]
    public async Task WorkerCanRetrieveOwnRatings()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.getratings@test.com", "Password123!", "Farmer GetRatings", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.getratings@test.com", "Password123!", "Worker GetRatings", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.getratings@test.com", "worker.getratings@test.com");

        await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 5, Comment: "Awesome work"));

        // Act
        var response = await workerClient.GetAsync("/api/worker/reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerRatingSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(5.0, summary.AverageRating);
        Assert.Equal(1, summary.TotalReviews);
        Assert.Single(summary.RecentReviews);
        Assert.Equal("Awesome work", summary.RecentReviews[0].Comment);
    }

    [Fact]
    public async Task WorkerCanSeeAverageRating()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.avgrating@test.com", "Password123!", "Farmer AvgRating", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.avgrating@test.com", "Password123!", "Worker AvgRating", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.avgrating@test.com", "worker.avgrating@test.com");

        await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 4, Comment: "Very Good"));

        // Act
        var response = await workerClient.GetAsync("/api/worker/reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerRatingSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(4.0, summary.AverageRating);
        Assert.Equal(1, summary.Breakdown.FourStars);
    }

    [Fact]
    public async Task WorkerCannotAccessAnotherWorkerRatings()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.ratingsA@test.com", "Password123!", "Worker A", Roles.Worker);
        await SetupTestUserAsync("worker.ratingsB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Act: Worker A fetches own ratings
        var response = await workerAClient.GetAsync("/api/worker/reviews");

        // Assert: Returns empty summary for Worker A
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerRatingSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalReviews);
        Assert.Empty(summary.RecentReviews);
    }

    [Fact]
    public async Task CreatingRatingCreatesWorkerNotification()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.notifreview@test.com", "Password123!", "Farmer NotifReview", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.notifreview@test.com", "Password123!", "Worker NotifReview", Roles.Worker);

        var setup = await SetupCompletedAssignmentAsync("farmer.notifreview@test.com", "worker.notifreview@test.com");

        // Act
        var rateResp = await farmerClient.PostAsJsonAsync($"/api/farmer/assignments/{setup.AssignmentId}/review", new CreateWorkerReviewRequest(Rating: 5, Comment: "Great work!"));
        Assert.Equal(HttpStatusCode.OK, rateResp.StatusCode);

        // Assert: Worker receives New Review Received notification
        var notifResp = await workerClient.GetAsync("/api/worker/notifications");
        Assert.Equal(HttpStatusCode.OK, notifResp.StatusCode);
        var notifs = await notifResp.Content.ReadFromJsonAsync<List<WorkerNotificationResponse>>(_jsonOptions);
        Assert.NotNull(notifs);
        Assert.Contains(notifs, n => n.Title.Contains("New Review Received", StringComparison.OrdinalIgnoreCase));
    }
}
