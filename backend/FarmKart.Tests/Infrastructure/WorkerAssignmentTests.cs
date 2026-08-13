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

public class WorkerAssignmentTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerAssignmentTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerAssignTest_{Guid.NewGuid()}";
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

    private async Task<(Guid FarmerUserId, Guid JobId, Guid ApplicationId, Guid WorkerUserId, Guid WorkerProfileId)> SeedJobAndApplicationAsync(
        string farmerEmail = "farmer.assignseed@test.com",
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
            Title = "Harvesting Cotton",
            Description = "Cotton harvesting work",
            WorkCategory = "Harvesting",
            CropType = "Cotton",
            WorkersRequired = workersRequired,
            RequiredExperience = 1,
            WagePerDay = 550,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            WorkingHours = "8 AM - 5 PM",
            FarmLocation = "Green Acres",
            Status = JobStatus.Open
        };
        db.Jobs.Add(job);

        var workerEmail = $"worker.{Guid.NewGuid()}@test.com";
        await SetupTestUserAsync(workerEmail, "Password123!", "Worker Assign Seed", Roles.Worker);
        var workerUser = await db.Users.SingleAsync(u => u.Email == workerEmail);
        var workerProfile = await db.WorkerProfiles.SingleAsync(p => p.UserId == workerUser.Id);

        var application = new JobApplication
        {
            JobId = job.Id,
            WorkerProfileId = workerProfile.Id,
            Status = ApplicationStatus.Pending,
            AppliedAtUtc = DateTime.UtcNow,
            Message = "Ready to harvest"
        };
        db.JobApplications.Add(application);

        await db.SaveChangesAsync();

        return (farmerUser.Id, job.Id, application.Id, workerUser.Id, workerProfile.Id);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public async Task AcceptingApplicationCreatesWorkerAssignment()
    {
        // Arrange
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.acceptcreate@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.acceptcreate@test.com", "Password123!", "Farmer Accept", Roles.Farmer);

        // Act
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var assignment = await db.WorkerAssignments.SingleOrDefaultAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);

        Assert.NotNull(assignment);
        Assert.Equal(AssignmentStatus.Active, assignment.Status);
        Assert.Equal(appId, assignment.JobApplicationId);
    }

    [Fact]
    public async Task PendingApplicationDoesNotHaveAssignment()
    {
        // Arrange
        var (_, jobId, _, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.pendingnoassign@test.com");

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var assignment = await db.WorkerAssignments.SingleOrDefaultAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);

        Assert.Null(assignment);
    }

    [Fact]
    public async Task RejectedApplicationDoesNotCreateAssignment()
    {
        // Arrange
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.rejectnoassign@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.rejectnoassign@test.com", "Password123!", "Farmer Reject", Roles.Farmer);

        // Act
        var response = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var assignment = await db.WorkerAssignments.SingleOrDefaultAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);

        Assert.Null(assignment);
    }

    [Fact]
    public async Task AssignmentBelongsToCorrectWorker()
    {
        // Arrange
        var (_, _, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.correctworker@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.correctworker@test.com", "Password123!", "Farmer CW", Roles.Farmer);

        // Act
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var assignment = await db.WorkerAssignments.SingleAsync(a => a.JobApplicationId == appId);

        Assert.Equal(workerProfileId, assignment.WorkerProfileId);
    }

    [Fact]
    public async Task AssignmentBelongsToCorrectJob()
    {
        // Arrange
        var (_, jobId, appId, _, _) = await SeedJobAndApplicationAsync("farmer.correctjob@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.correctjob@test.com", "Password123!", "Farmer CJ", Roles.Farmer);

        // Act
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var assignment = await db.WorkerAssignments.SingleAsync(a => a.JobApplicationId == appId);

        Assert.Equal(jobId, assignment.JobId);
    }

    [Fact]
    public async Task FarmerCanViewAssignmentsForOwnJob()
    {
        // Arrange
        var (_, jobId, appId, _, _) = await SeedJobAndApplicationAsync("farmer.viewownassign@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.viewownassign@test.com", "Password123!", "Farmer ViewAssign", Roles.Farmer);
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Act
        var response = await farmerClient.GetAsync($"/api/farmer/jobs/{jobId}/assignments");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignments = await response.Content.ReadFromJsonAsync<List<FarmerWorkerAssignmentResponse>>(_jsonOptions);
        Assert.NotNull(assignments);
        Assert.Single(assignments);
        Assert.Equal(jobId, assignments[0].JobId);
        Assert.Equal(AssignmentStatus.Active, assignments[0].Status);
    }

    [Fact]
    public async Task FarmerCannotViewAssignmentsForAnotherFarmersJob()
    {
        // Arrange
        var (_, jobId, appId, _, _) = await SeedJobAndApplicationAsync("farmer.ownerAassign@test.com");
        var farmerAClient = await GetAuthenticatedClientAsync("farmer.ownerAassign@test.com", "Password123!", "Farmer A", Roles.Farmer);
        await farmerAClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        var farmerBClient = await GetAuthenticatedClientAsync("farmer.ownerBassign@test.com", "Password123!", "Farmer B", Roles.Farmer);

        // Act
        var response = await farmerBClient.GetAsync($"/api/farmer/jobs/{jobId}/assignments");

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCanViewOwnAssignments()
    {
        // Arrange
        var (_, _, appId, _, _) = await SeedJobAndApplicationAsync("farmer.workerownassign@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.workerownassign@test.com", "Password123!", "Farmer WorkerOwn", Roles.Farmer);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var app = await db.JobApplications.Include(a => a.WorkerProfile).SingleAsync(a => a.Id == appId);
        var workerUser = await db.Users.SingleAsync(u => u.Id == app.WorkerProfile.UserId);
        var workerEmail = workerUser.Email!;

        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        var workerClient = await GetAuthenticatedClientAsync(workerEmail, "Password123!", "Worker Seed", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync("/api/worker/assignments");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignments = await response.Content.ReadFromJsonAsync<List<WorkerAssignmentResponse>>(_jsonOptions);
        Assert.NotNull(assignments);
        Assert.Single(assignments);
        Assert.Equal(AssignmentStatus.Active, assignments[0].Status);
    }

    [Fact]
    public async Task WorkerCannotViewAnotherWorkersAssignment()
    {
        // Arrange
        var (_, _, appId, _, _) = await SeedJobAndApplicationAsync("farmer.anotherworkerassign@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.anotherworkerassign@test.com", "Password123!", "Farmer AnotherW", Roles.Farmer);
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        Guid assignmentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            assignmentId = (await db.WorkerAssignments.SingleAsync(a => a.JobApplicationId == appId)).Id;
        }

        var otherWorkerClient = await GetAuthenticatedClientAsync($"otherworker.{Guid.NewGuid()}@test.com", "Password123!", "Other Worker", Roles.Worker);

        // Act
        var response = await otherWorkerClient.GetAsync($"/api/worker/assignments/{assignmentId}");

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateAssignmentIsRejected()
    {
        // Arrange
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.dupassign@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.dupassign@test.com", "Password123!", "Farmer Dup", Roles.Farmer);
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Act: Manually try to accept again or trigger duplicate assignment creation
        var secondRes = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotCreateOrAssignThemselves()
    {
        // Arrange
        var (_, _, appId, _, _) = await SeedJobAndApplicationAsync("farmer.selfassign@test.com");
        var workerClient = await GetAuthenticatedClientAsync("worker.attemptassign@test.com", "Password123!", "Worker Self", Roles.Worker);

        // Act
        var response = await workerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotAccessAssignmentApis()
    {
        // Arrange
        var (_, jobId, _, _, _) = await SeedJobAndApplicationAsync("farmer.custassign@test.com");
        var customerClient = await GetAuthenticatedClientAsync("customer.attemptassign@test.com", "Password123!", "Customer Assign", Roles.Customer);

        // Act 1: Farmer API
        var res1 = await customerClient.GetAsync($"/api/farmer/jobs/{jobId}/assignments");
        // Act 2: Worker API
        var res2 = await customerClient.GetAsync("/api/worker/assignments");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, res1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, res2.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUserReceives401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/assignments");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmerOwnershipIsVerifiedServerSide()
    {
        // Arrange: Job owned by Farmer A
        var (_, jobId, appId, _, _) = await SeedJobAndApplicationAsync("farmer.realownerassign@test.com");

        // Farmer B attempts to view assignments for Farmer A's job
        var farmerBClient = await GetAuthenticatedClientAsync("farmer.imposterassign@test.com", "Password123!", "Farmer Imposter", Roles.Farmer);

        // Act
        var response = await farmerBClient.GetAsync($"/api/farmer/jobs/{jobId}/assignments");

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkerOwnershipIsVerifiedServerSide()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.verified@test.com", "Password123!", "Verified Worker", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync($"/api/worker/assignments/{Guid.NewGuid()}");

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CapacityRulesAreRespected()
    {
        // Arrange: Job requires 1 worker
        var (_, jobId, appId1, _, _) = await SeedJobAndApplicationAsync("farmer.capacityassign@test.com", workersRequired: 1);

        // Seed second application
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

        var farmerClient = await GetAuthenticatedClientAsync("farmer.capacityassign@test.com", "Password123!", "Farmer Cap", Roles.Farmer);

        // Accept 1st app -> 1 assignment created
        var res1 = await farmerClient.PostAsync($"/api/farmer/applications/{appId1}/accept", null);
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // Try accepting 2nd app -> exceeds capacity limit
        var res2 = await farmerClient.PostAsync($"/api/farmer/applications/{appId2}/accept", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task ExistingAcceptedApplicationWithoutAssignment_CreatesAssignment()
    {
        // Arrange: Seed an Accepted JobApplication without a WorkerAssignment (simulating legacy data)
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.backfill1@test.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var app = await db.JobApplications.SingleAsync(a => a.Id == appId);
            app.Status = ApplicationStatus.Accepted;
            await db.SaveChangesAsync();

            // Ensure no assignment exists yet
            var existingAssign = await db.WorkerAssignments.SingleOrDefaultAsync(a => a.JobApplicationId == appId);
            Assert.Null(existingAssign);

            // Act: Run backfill sync
            await FarmKart.Infrastructure.Persistence.Seeding.AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(db);

            // Assert: Missing assignment is created
            var createdAssign = await db.WorkerAssignments.SingleOrDefaultAsync(a => a.JobApplicationId == appId);
            Assert.NotNull(createdAssign);
            Assert.Equal(AssignmentStatus.Active, createdAssign.Status);
            Assert.Equal(jobId, createdAssign.JobId);
            Assert.Equal(workerProfileId, createdAssign.WorkerProfileId);
        }
    }

    [Fact]
    public async Task ExistingAcceptedApplicationWithAssignment_DoesNotCreateDuplicate()
    {
        // Arrange: Seed Accepted application with existing WorkerAssignment
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.backfill2@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.backfill2@test.com", "Password123!", "Farmer BF2", Roles.Farmer);
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var countBefore = await db.WorkerAssignments.CountAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);
            Assert.Equal(1, countBefore);

            // Act: Run backfill sync again (idempotency check)
            await FarmKart.Infrastructure.Persistence.Seeding.AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(db);

            // Assert: Count remains 1, no duplicate assignment created
            var countAfter = await db.WorkerAssignments.CountAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);
            Assert.Equal(1, countAfter);
        }
    }

    [Fact]
    public async Task PendingApplication_DoesNotCreateAssignment()
    {
        // Arrange: Seed Pending application
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.backfillpending@test.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

            // Act: Run backfill sync
            await FarmKart.Infrastructure.Persistence.Seeding.AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(db);

            // Assert: No assignment is created
            var count = await db.WorkerAssignments.CountAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task RejectedApplication_DoesNotCreateAssignment()
    {
        // Arrange: Seed Rejected application
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.backfillrejected@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.backfillrejected@test.com", "Password123!", "Farmer Rej", Roles.Farmer);
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/reject", null);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

            // Act: Run backfill sync
            await FarmKart.Infrastructure.Persistence.Seeding.AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(db);

            // Assert: No assignment is created
            var count = await db.WorkerAssignments.CountAsync(a => a.JobId == jobId && a.WorkerProfileId == workerProfileId);
            Assert.Equal(0, count);
        }
    }

    [Fact]
    public async Task FutureAccept_AutomaticallyCreatesAssignment()
    {
        // Arrange: Seed Pending application
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.futureaccept@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.futureaccept@test.com", "Password123!", "Farmer Future", Roles.Farmer);

        // Act: Accept application via API
        var res = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Assert: Assignment is automatically created
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var assignment = await db.WorkerAssignments.SingleOrDefaultAsync(a => a.JobApplicationId == appId);
        Assert.NotNull(assignment);
        Assert.Equal(AssignmentStatus.Active, assignment.Status);
    }

    [Fact]
    public async Task DuplicateAssignment_IsPrevented()
    {
        // Arrange: Seed Accepted application with existing assignment
        var (_, jobId, appId, _, workerProfileId) = await SeedJobAndApplicationAsync("farmer.dupcheck@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.dupcheck@test.com", "Password123!", "Farmer DupCheck", Roles.Farmer);
        await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Act: Try accepting again
        var secondRes = await farmerClient.PostAsync($"/api/farmer/applications/{appId}/accept", null);

        // Assert: 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, secondRes.StatusCode);
    }
}
