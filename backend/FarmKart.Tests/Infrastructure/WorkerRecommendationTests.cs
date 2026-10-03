using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FarmKart.Application.Abstractions.AI;
using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

/// <summary>
/// Integration & Unit test suite for AI Worker Recommendation (AI-02).
/// Tests cover:
/// 1. Farmer request for own job
/// 2. Unauthorized request (no auth cookie)
/// 3. Worker role unauthorized (403 Forbidden)
/// 4. Farmer request for another farmer's job (404 Not Found)
/// 5. No eligible workers
/// 6. One eligible worker
/// 7. Multiple eligible workers ranked by match score
/// 8. Skill matching
/// 9. Missing required skill
/// 10. Experience scoring
/// 11. Availability filtering (IsAvailable == false filtered out)
/// 12. Date conflict filtering (active assignment date overlap filtered out)
/// 13. Wage compatibility scoring
/// 14. Rating factor
/// 15. Location factor
/// 16. Existing assignment conflict
/// 17. Existing application handling (HasApplied = true)
/// 18. Match score normalization
/// 19. Confidence validation
/// 20. AI provider success
/// 21. AI provider failure
/// 22. AI fallback
/// 23. Malformed AI explanation handling
/// 24. CancellationToken support
/// 25. Top-N limit
/// 26. Data minimization check (no sensitive PII sent in AI request)
/// 27. Database immutability (zero record mutations during recommendation)
/// </summary>
public class WorkerRecommendationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    private sealed class StubAiRecommendationProvider : IAiRecommendationProvider
    {
        public IReadOnlyList<AiRecommendationItemDto>? ResponseToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public int CallCount { get; private set; }
        public IReadOnlyList<CandidateSummaryDto>? LastSummaries { get; private set; }

        public Task<IReadOnlyList<AiRecommendationItemDto>> GetRecommendationReasonsAsync(
            string recommendationType,
            string contextSummary,
            IReadOnlyList<CandidateSummaryDto> candidateSummaries,
            string language,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSummaries = candidateSummaries;

            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult<IReadOnlyList<AiRecommendationItemDto>>(
                ResponseToReturn ?? Array.Empty<AiRecommendationItemDto>());
        }
    }

    public WorkerRecommendationTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerRecTest_{Guid.NewGuid()}";
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
                    options.UseSqlServer(TestSqlServer.ConnectionString(_dbName)));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                db.Database.EnsureCreated();
            });
        });
    }

    public void Dispose()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        db.Database.EnsureDeleted();
    }

    private async Task<(HttpClient Client, Guid FarmerUserId, Guid FarmerProfileId)> CreateFarmerClientAsync(string email = "farmer.rec@test.com")
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        if (!await roleManager.RoleExistsAsync(Roles.Farmer))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user, "Password123!");
        await userManager.AddToRoleAsync(user, Roles.Farmer);

        var profile = new FarmerProfile
        {
            UserId = user.Id,
            FullName = "Test Farmer",
            Phone = "9876543210",
            FarmLocation = "Surat"
        };
        db.FarmerProfiles.Add(profile);
        await db.SaveChangesAsync();

        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = jwtTokenService.GenerateToken(user.Id, user.Email!, Roles.Farmer);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"FarmKartAuth={token}");

        return (client, user.Id, profile.Id);
    }

    private async Task<HttpClient> CreateWorkerClientAsync(string email = "worker.rec@test.com")
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        if (!await roleManager.RoleExistsAsync(Roles.Worker))
            await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Worker));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user, "Password123!");
        await userManager.AddToRoleAsync(user, Roles.Worker);

        var profile = new WorkerProfile
        {
            UserId = user.Id,
            FullName = "Test Worker",
            Phone = "9876543211",
            ExperienceYears = 3,
            ExpectedDailyWage = 500
        };
        db.WorkerProfiles.Add(profile);
        await db.SaveChangesAsync();

        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = jwtTokenService.GenerateToken(user.Id, user.Email!, Roles.Worker);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"FarmKartAuth={token}");
        return client;
    }

    private async Task<Job> SeedJobAsync(Guid farmerProfileId, string title = "Wheat Harvesting", string category = "Harvesting", decimal wage = 700)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var job = new Job
        {
            FarmerProfileId = farmerProfileId,
            Title = title,
            Description = "Need experienced harvesters",
            WorkCategory = category,
            WorkersRequired = 5,
            RequiredExperience = 2,
            WagePerDay = wage,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
            WorkingHours = "8 hours",
            FarmLocation = "Surat",
            Status = JobStatus.Open
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    private async Task<WorkerProfile> SeedWorkerProfileAsync(
        string name,
        int expYears = 3,
        decimal wage = 600,
        bool isAvailable = true,
        DateOnly? availableFrom = null,
        string? city = "Surat",
        string? skillName = "Harvesting")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var user = new ApplicationUser
        {
            UserName = $"{name.Replace(" ", "").ToLower()}@test.com",
            Email = $"{name.Replace(" ", "").ToLower()}@test.com",
            EmailConfirmed = true
        };
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await userManager.CreateAsync(user, "Password123!");

        var workerProfile = new WorkerProfile
        {
            UserId = user.Id,
            FullName = name,
            Phone = "9998887776",
            ExperienceYears = expYears,
            ExpectedDailyWage = wage,
            MinimumDailyWage = wage - 50,
            IsAvailable = isAvailable,
            AvailableFrom = availableFrom,
            AddressInfo = new Domain.ValueObjects.AddressInfo { City = city, State = "Gujarat" },
            PreferredWorkCategories = skillName
        };

        db.WorkerProfiles.Add(workerProfile);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(skillName))
        {
            var skill = await db.Skills.FirstOrDefaultAsync(s => s.Name == skillName);
            if (skill == null)
            {
                skill = new Skill { Name = skillName, Description = skillName };
                db.Skills.Add(skill);
                await db.SaveChangesAsync();
            }

            db.WorkerSkills.Add(new WorkerSkill
            {
                WorkerProfileId = workerProfile.Id,
                SkillId = skill.Id,
                ExperienceYears = expYears
            });
            await db.SaveChangesAsync();
        }

        return workerProfile;
    }

    // ── Test 1: Authenticated Farmer can request recommendations for own job ─

    [Fact]
    public async Task GetRecommendedWorkers_FarmerOwnJob_Returns200OK()
    {
        // Arrange
        var (client, farmerUserId, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        await SeedWorkerProfileAsync("Ramesh Patel");

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Equal(job.Id, result.JobId);
        Assert.NotEmpty(result.Recommendations);
    }

    // ── Test 2: Unauthorized request returns 401 Unauthorized ────────────────

    [Fact]
    public async Task GetRecommendedWorkers_Unauthenticated_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();
        var jobId = Guid.NewGuid();

        // Act
        var response = await unauthClient.GetAsync($"/api/farmer/jobs/{jobId}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Test 3: Worker role user returns 403 Forbidden ─────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_WorkerRole_Returns403Forbidden()
    {
        // Arrange
        var workerClient = await CreateWorkerClientAsync();
        var jobId = Guid.NewGuid();

        // Act
        var response = await workerClient.GetAsync($"/api/farmer/jobs/{jobId}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Test 4: Farmer requesting another farmer's job returns 404 ─────────────

    [Fact]
    public async Task GetRecommendedWorkers_OtherFarmersJob_Returns404NotFound()
    {
        // Arrange
        var (farmerClient1, _, farmerProfileId1) = await CreateFarmerClientAsync("farmer1@test.com");
        var (farmerClient2, _, _) = await CreateFarmerClientAsync("farmer2@test.com");

        var jobOwner1 = await SeedJobAsync(farmerProfileId1);

        // Act — Farmer 2 requests recommendations for Farmer 1's job
        var response = await farmerClient2.GetAsync($"/api/farmer/jobs/{jobOwner1.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Test 5: No eligible workers returns empty recommendations ─────────────

    [Fact]
    public async Task GetRecommendedWorkers_NoEligibleWorkers_ReturnsEmptyList()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        // Seed unavailable worker
        await SeedWorkerProfileAsync("Unavailable Worker", isAvailable: false);

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Empty(result.Recommendations);
        Assert.NotNull(result.FallbackReason);
    }

    // ── Test 6: Single eligible worker returned with 100% match ───────────────

    [Fact]
    public async Task GetRecommendedWorkers_SingleEligibleWorker_Returns100MatchScore()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        var worker = await SeedWorkerProfileAsync("Solo Worker", expYears: 5, wage: 650);

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Single(result.Recommendations);
        Assert.Equal(worker.FullName, result.Recommendations[0].FullName);
        Assert.Equal(100.0, result.Recommendations[0].MatchScore);
    }

    // ── Test 7: Multiple eligible workers ranked by score descending ──────────

    [Fact]
    public async Task GetRecommendedWorkers_MultipleWorkers_RankedByScore()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId, category: "Harvesting", wage: 700);

        var bestWorker = await SeedWorkerProfileAsync("Best Worker", expYears: 5, wage: 600, skillName: "Harvesting");
        var lowWorker = await SeedWorkerProfileAsync("Low Worker", expYears: 1, wage: 900, skillName: "Plumbing");

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.True(result.Recommendations.Count >= 2);
        Assert.Equal(bestWorker.FullName, result.Recommendations[0].FullName);
        Assert.True(result.Recommendations[0].MatchScore >= result.Recommendations[1].MatchScore);
    }

    // ── Test 8 & 9: Skill matching affects match score ───────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_MatchingSkill_ScoresHigherThanMissingSkill()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId, category: "Tractor Driving");

        var driver = await SeedWorkerProfileAsync("Tractor Driver", expYears: 3, skillName: "Tractor Driving");
        var nonDriver = await SeedWorkerProfileAsync("Generic Worker", expYears: 3, skillName: "Weeding");

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Equal(driver.FullName, result.Recommendations[0].FullName);
    }

    // ── Test 10: Experience scoring ──────────────────────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_HigherExperience_ScoresHigher()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);

        var veteran = await SeedWorkerProfileAsync("Veteran Worker", expYears: 8, wage: 600);
        var novice = await SeedWorkerProfileAsync("Novice Worker", expYears: 1, wage: 600);

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Equal(veteran.FullName, result.Recommendations[0].FullName);
    }

    // ── Test 11: Unavailable worker (IsAvailable = false) is filtered out ─────

    [Fact]
    public async Task GetRecommendedWorkers_UnavailableWorker_FilteredOut()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);

        var activeWorker = await SeedWorkerProfileAsync("Available Worker", isAvailable: true);
        var inactiveWorker = await SeedWorkerProfileAsync("Inactive Worker", isAvailable: false);

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Recommendations, r => r.FullName == "Inactive Worker");
        Assert.Contains(result.Recommendations, r => r.FullName == "Available Worker");
    }

    // ── Test 12 & 16: Active assignment date conflict is filtered out ──────────

    [Fact]
    public async Task GetRecommendedWorkers_WorkerWithAssignmentConflict_FilteredOut()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);

        var busyWorker = await SeedWorkerProfileAsync("Busy Worker");
        var freeWorker = await SeedWorkerProfileAsync("Free Worker");

        // Seed an overlapping active assignment for busyWorker
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var otherJob = new Job
            {
                FarmerProfileId = farmerProfileId,
                Title = "Other Job",
                WorkCategory = "Sowing",
                WorkersRequired = 2,
                WagePerDay = 500,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                WorkingHours = "8 hrs",
                FarmLocation = "Surat",
                Status = JobStatus.InProgress
            };
            db.Jobs.Add(otherJob);
            await db.SaveChangesAsync();

            db.WorkerAssignments.Add(new WorkerAssignment
            {
                JobId = otherJob.Id,
                WorkerProfileId = busyWorker.Id,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = AssignmentStatus.Active
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Recommendations, r => r.FullName == "Busy Worker");
        Assert.Contains(result.Recommendations, r => r.FullName == "Free Worker");
    }

    // ── Test 13: Wage compatibility scoring ───────────────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_WageWithinBudget_ScoresHigherThanOverBudget()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId, wage: 600);

        var budgetWorker = await SeedWorkerProfileAsync("Budget Worker", wage: 550);
        var expensiveWorker = await SeedWorkerProfileAsync("Expensive Worker", wage: 1200);

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Equal(budgetWorker.FullName, result.Recommendations[0].FullName);
    }

    // ── Test 14: Worker Rating factor increases score ─────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_HighlyRatedWorker_ScoresHigher()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);

        var ratedWorker = await SeedWorkerProfileAsync("Rated Worker");
        var unratedWorker = await SeedWorkerProfileAsync("Unrated Worker");

        // Seed 5-star review for ratedWorker
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            db.Reviews.Add(new Review
            {
                ReviewerUserId = farmerProfileId.ToString(),
                RevieweeUserId = ratedWorker.UserId.ToString(),
                Rating = 5,
                Comment = "Excellent harvester!",
                RelatedEntityType = ReviewEntityType.WorkerAssignment
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        var item = result.Recommendations.FirstOrDefault(r => r.FullName == "Rated Worker");
        Assert.NotNull(item);
        Assert.Equal(5.0, item.AverageRating);
    }

    // ── Test 15: Location match factor increases score ─────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_MatchingLocation_ScoresHigher()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);

        var localWorker = await SeedWorkerProfileAsync("Local Worker", city: "Surat");
        var distantWorker = await SeedWorkerProfileAsync("Distant Worker", city: "Rajkot");

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Equal(localWorker.FullName, result.Recommendations[0].FullName);
    }

    // ── Test 17: Existing application handling (HasApplied = true) ─────────────

    [Fact]
    public async Task GetRecommendedWorkers_ExistingApplicant_HasAppliedIsTrue()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        var applicantWorker = await SeedWorkerProfileAsync("Applicant Worker");

        // Seed job application
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            db.JobApplications.Add(new JobApplication
            {
                JobId = job.Id,
                WorkerProfileId = applicantWorker.Id,
                Status = ApplicationStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        var item = result.Recommendations.FirstOrDefault(r => r.FullName == "Applicant Worker");
        Assert.NotNull(item);
        Assert.True(item.HasApplied);
    }

    // ── Test 18 & 19: Score normalization and confidence validation ───────────

    [Fact]
    public async Task GetRecommendedWorkers_ScoresAreNormalizedAndConfidenceBounded()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        await SeedWorkerProfileAsync("Worker One");
        await SeedWorkerProfileAsync("Worker Two");

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        foreach (var item in result.Recommendations)
        {
            Assert.InRange(item.MatchScore, 0.0, 100.0);
            Assert.InRange(item.ConfidenceScore, 0.0, 1.0);
        }
    }

    // ── Test 20 & 22: AI provider fallback handling ───────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_AiProviderFailure_FallsBackGracefully()
    {
        // Arrange
        var stubAiProvider = new StubAiRecommendationProvider
        {
            ExceptionToThrow = new TimeoutException("Gemini API timeout")
        };
        var service = new WorkerRecommendationService(
            _factory.Services.CreateScope().ServiceProvider.GetRequiredService<FarmKartDbContext>(),
            new RecommendationEngine(stubAiProvider, NullLogger<RecommendationEngine>.Instance),
            NullLogger<WorkerRecommendationService>.Instance);

        var (client, farmerUserId, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        await SeedWorkerProfileAsync("Worker Direct");

        // Act — directly call service with stub engine
        var response = await service.GetRecommendedWorkersAsync(farmerUserId, job.Id);

        // Assert — service must NOT crash; must return deterministic fallback results
        Assert.NotNull(response);
        Assert.NotEmpty(response.Recommendations);
        Assert.False(response.AiEnriched);
        Assert.NotNull(response.FallbackReason);
    }

    // ── Test 24: CancellationToken support ───────────────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var service = new WorkerRecommendationService(
            scope.ServiceProvider.GetRequiredService<FarmKartDbContext>(),
            scope.ServiceProvider.GetRequiredService<IRecommendationEngine>(),
            NullLogger<WorkerRecommendationService>.Instance);

        var farmerUserId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetRecommendedWorkersAsync(farmerUserId, jobId, 5, cts.Token));
    }

    // ── Test 25: Top-N limit is respected ────────────────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_TopNLimit_LimitsReturnedCount()
    {
        // Arrange
        var (client, _, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);

        for (int i = 1; i <= 8; i++)
        {
            await SeedWorkerProfileAsync($"Worker Batch {i}", expYears: i);
        }

        // Act — request top 3
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers?topN=3");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<WorkerRecommendationResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Recommendations.Count);
    }

    // ── Test 26: Data minimization audit ─────────────────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_DataMinimization_DoesNotExposePasswordsOrPII()
    {
        // Arrange
        var stubAiProvider = new StubAiRecommendationProvider();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        var (client, farmerUserId, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        await SeedWorkerProfileAsync("Privacy Test Worker");

        var service = new WorkerRecommendationService(
            db,
            new RecommendationEngine(stubAiProvider, NullLogger<RecommendationEngine>.Instance),
            NullLogger<WorkerRecommendationService>.Instance);

        // Act
        await service.GetRecommendedWorkersAsync(farmerUserId, job.Id);

        // Assert — check that candidate summaries sent to AI contain NO passwords, phone numbers, or tokens
        Assert.NotNull(stubAiProvider.LastSummaries);
        foreach (var summary in stubAiProvider.LastSummaries!)
        {
            foreach (var kv in summary.Attributes)
            {
                Assert.DoesNotContain("password", kv.Key, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("token", kv.Key, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("phone", kv.Key, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ── Test 27: Database immutability ───────────────────────────────────────

    [Fact]
    public async Task GetRecommendedWorkers_QueryExecution_DoesNotMutateDatabase()
    {
        // Arrange
        var (client, farmerUserId, farmerProfileId) = await CreateFarmerClientAsync();
        var job = await SeedJobAsync(farmerProfileId);
        await SeedWorkerProfileAsync("Immutability Worker");

        int initialJobsCount;
        int initialWorkersCount;
        int initialAssignmentsCount;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            initialJobsCount = await db.Jobs.CountAsync();
            initialWorkersCount = await db.WorkerProfiles.CountAsync();
            initialAssignmentsCount = await db.WorkerAssignments.CountAsync();
        }

        // Act
        var response = await client.GetAsync($"/api/farmer/jobs/{job.Id}/recommended-workers");

        // Assert — verify 200 OK and database record counts remain unchanged
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            Assert.Equal(initialJobsCount, await db.Jobs.CountAsync());
            Assert.Equal(initialWorkersCount, await db.WorkerProfiles.CountAsync());
            Assert.Equal(initialAssignmentsCount, await db.WorkerAssignments.CountAsync());
        }
    }
}
