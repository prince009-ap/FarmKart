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

public class AttendanceTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public AttendanceTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_AttendanceTest_{Guid.NewGuid()}";
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

    private async Task<(Guid FarmerUserId, Guid JobId, Guid AssignmentId, Guid WorkerUserId, Guid WorkerProfileId)> SeedAssignmentAsync(
        string farmerEmail = "farmer.attendseed@test.com",
        int startDaysOffset = -5,
        int endDaysOffset = 5)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();

        await SetupTestUserAsync(farmerEmail, "Password123!", "Farmer Attend Seed", Roles.Farmer);
        var farmerUser = await db.Users.SingleAsync(u => u.Email == farmerEmail);
        var farmerProfile = await db.FarmerProfiles.SingleAsync(p => p.UserId == farmerUser.Id);

        var job = new Job
        {
            FarmerProfileId = farmerProfile.Id,
            Title = "Irrigation Work",
            Description = "Irrigation work for crops",
            WorkCategory = "Irrigation",
            CropType = "Wheat",
            WorkersRequired = 2,
            RequiredExperience = 1,
            WagePerDay = 500,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(startDaysOffset)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(endDaysOffset)),
            WorkingHours = "8 AM - 5 PM",
            FarmLocation = "Green Field",
            Status = JobStatus.Open
        };
        db.Jobs.Add(job);

        var workerEmail = $"worker.{Guid.NewGuid()}@test.com";
        await SetupTestUserAsync(workerEmail, "Password123!", "Worker Attend Seed", Roles.Worker);
        var workerUser = await db.Users.SingleAsync(u => u.Email == workerEmail);
        var workerProfile = await db.WorkerProfiles.SingleAsync(p => p.UserId == workerUser.Id);

        var application = new JobApplication
        {
            JobId = job.Id,
            WorkerProfileId = workerProfile.Id,
            Status = ApplicationStatus.Accepted,
            AppliedAtUtc = DateTime.UtcNow
        };
        db.JobApplications.Add(application);

        var assignment = new WorkerAssignment
        {
            JobId = job.Id,
            WorkerProfileId = workerProfile.Id,
            JobApplicationId = application.Id,
            AssignedAtUtc = DateTime.UtcNow,
            StartDate = job.StartDate,
            EndDate = job.EndDate,
            Status = AssignmentStatus.Active
        };
        db.WorkerAssignments.Add(assignment);

        await db.SaveChangesAsync();

        return (farmerUser.Id, job.Id, assignment.Id, workerUser.Id, workerProfile.Id);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public async Task FarmerCanViewAttendanceForOwnJob()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.viewattend@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.viewattend@test.com", "Password123!", "Farmer ViewAttend", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync($"/api/farmer/jobs/{jobId}/attendance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCanMarkAssignedWorkerPresent()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.markpresent@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.markpresent@test.com", "Password123!", "Farmer Present", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var req = new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present, "Worked full day", null, null, 8m)
        });

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<FarmerAttendanceResponse>>(_jsonOptions);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(AttendanceStatus.Present, list[0].Status);

        // Verify DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var record = await db.Attendances.SingleAsync(a => a.WorkerAssignmentId == assignmentId && a.Date == date);
        Assert.Equal(AttendanceStatus.Present, record.Status);
    }

    [Fact]
    public async Task FarmerCanMarkAssignedWorkerAbsent()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.markabsent@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.markabsent@test.com", "Password123!", "Farmer Absent", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var req = new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Absent, "Did not report", null, null, 0m)
        });

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<FarmerAttendanceResponse>>(_jsonOptions);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(AttendanceStatus.Absent, list[0].Status);
    }

    [Fact]
    public async Task FarmerCanUpdateExistingAttendance()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.updateattend@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.updateattend@test.com", "Password123!", "Farmer Update", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Mark Absent first
        var req1 = new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Absent, "Initial absent", null, null, 0m)
        });
        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req1);

        // Act: Update to Present
        var req2 = new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present, "Updated to present", null, null, 8m)
        });
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req2);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var record = await db.Attendances.SingleAsync(a => a.WorkerAssignmentId == assignmentId && a.Date == date);
        Assert.Equal(AttendanceStatus.Present, record.Status);
    }

    [Fact]
    public async Task FutureAttendanceIsRejected()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.futurereject@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.futurereject@test.com", "Password123!", "Farmer FutureReject", Roles.Farmer);
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var req = new SaveJobAttendanceRequest(futureDate, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        });

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AttendanceBeforeAssignmentStartDateIsRejected()
    {
        // Arrange: Job starts today
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.prestartreject@test.com", startDaysOffset: 0, endDaysOffset: 5);
        var farmerClient = await GetAuthenticatedClientAsync("farmer.prestartreject@test.com", "Password123!", "Farmer PreStart", Roles.Farmer);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var req = new SaveJobAttendanceRequest(yesterday, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        });

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AttendanceAfterAssignmentEndDateIsRejected()
    {
        // Arrange: Job ended yesterday
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.postendreject@test.com", startDaysOffset: -5, endDaysOffset: -1);
        var farmerClient = await GetAuthenticatedClientAsync("farmer.postendreject@test.com", "Password123!", "Farmer PostEnd", Roles.Farmer);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var req = new SaveJobAttendanceRequest(today, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        });

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotMarkAttendanceForAnotherFarmersAssignment()
    {
        // Arrange: Farmer A owns assignment
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.ownerAattend@test.com");

        // Farmer B attempts to mark attendance for Farmer A's job
        var farmerBClient = await GetAuthenticatedClientAsync("farmer.ownerBattend@test.com", "Password123!", "Farmer B", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var req = new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        });

        // Act
        var response = await farmerBClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotMarkAttendanceForUnassignedWorker()
    {
        // Arrange
        var (_, jobId, _, _, _) = await SeedAssignmentAsync("farmer.unassignedworker@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.unassignedworker@test.com", "Password123!", "Farmer Unassigned", Roles.Farmer);
        var invalidAssignmentId = Guid.NewGuid();

        var req = new SaveJobAttendanceRequest(DateOnly.FromDateTime(DateTime.UtcNow), new List<MarkAttendanceItemRequest>
        {
            new(invalidAssignmentId, AttendanceStatus.Present)
        });

        // Act
        var response = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCanViewOwnAttendance()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.workerviewattend@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.workerviewattend@test.com", "Password123!", "Farmer WVA", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Mark Present
        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        }));

        string workerEmail;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var assignment = await db.WorkerAssignments.Include(w => w.WorkerProfile).SingleAsync(w => w.Id == assignmentId);
            var workerUser = await db.Users.SingleAsync(u => u.Id == assignment.WorkerProfile.UserId);
            workerEmail = workerUser.Email!;
        }

        var workerClient = await GetAuthenticatedClientAsync(workerEmail, "Password123!", "Worker View", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync("/api/worker/attendance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerAttendanceSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalDays);
        Assert.Equal(1, summary.PresentDays);
        Assert.Equal(100m, summary.AttendancePercentage);
    }

    [Fact]
    public async Task WorkerCannotViewAnotherWorkersAttendance()
    {
        // Arrange
        var (_, _, assignmentId, _, _) = await SeedAssignmentAsync("farmer.otherworkerattend@test.com");
        var otherWorkerClient = await GetAuthenticatedClientAsync($"otherworker.{Guid.NewGuid()}@test.com", "Password123!", "Other Worker", Roles.Worker);

        // Act
        var response = await otherWorkerClient.GetAsync($"/api/worker/assignments/{assignmentId}/attendance");

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotModifyAttendance()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.workermodify@test.com");
        var workerClient = await GetAuthenticatedClientAsync("worker.attempthack@test.com", "Password123!", "Worker Hack", Roles.Worker);

        var req = new SaveJobAttendanceRequest(DateOnly.FromDateTime(DateTime.UtcNow), new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        });

        // Act
        var response = await workerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUserReceives401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/attendance");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WorkerAccessingFarmerAttendanceApiReceives403()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.farmercall@test.com", "Password123!", "Worker Call", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync($"/api/farmer/jobs/{Guid.NewGuid()}/attendance");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerReceives403()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.call@test.com", "Password123!", "Customer Call", Roles.Customer);

        // Act 1: Farmer API
        var res1 = await customerClient.GetAsync($"/api/farmer/jobs/{Guid.NewGuid()}/attendance");
        // Act 2: Worker API
        var res2 = await customerClient.GetAsync("/api/worker/attendance");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, res1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, res2.StatusCode);
    }

    [Fact]
    public async Task DuplicateAttendanceForSameAssignmentDateIsPrevented()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.dupattend@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.dupattend@test.com", "Password123!", "Farmer Dup", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Post twice for same date
        var req = new SaveJobAttendanceRequest(date, new List<MarkAttendanceItemRequest>
        {
            new(assignmentId, AttendanceStatus.Present)
        });

        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);
        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);

        // Assert DB count is exactly 1
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
        var count = await db.Attendances.CountAsync(a => a.WorkerAssignmentId == assignmentId && a.Date == date);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task InvalidAttendanceStatusIsRejected()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.invalidstatus@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.invalidstatus@test.com", "Password123!", "Farmer Invalid", Roles.Farmer);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        // Post invalid enum (999)
        var invalidJson = $$"""
        {
            "date": "{{date:yyyy-MM-dd}}",
            "items": [
                {
                    "workerAssignmentId": "{{assignmentId}}",
                    "status": 999
                }
            ]
        }
        """;

        var content = new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await farmerClient.PostAsync($"/api/farmer/jobs/{jobId}/attendance", content);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AttendanceSummaryCalculationsAreCorrect()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.summarycalc@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.summarycalc@test.com", "Password123!", "Farmer Summary", Roles.Farmer);

        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", new SaveJobAttendanceRequest(date1, new List<MarkAttendanceItemRequest> { new(assignmentId, AttendanceStatus.Present) }));
        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", new SaveJobAttendanceRequest(date2, new List<MarkAttendanceItemRequest> { new(assignmentId, AttendanceStatus.Absent) }));

        string workerEmail;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var assignment = await db.WorkerAssignments.Include(w => w.WorkerProfile).SingleAsync(w => w.Id == assignmentId);
            var workerUser = await db.Users.SingleAsync(u => u.Id == assignment.WorkerProfile.UserId);
            workerEmail = workerUser.Email!;
        }

        var workerClient = await GetAuthenticatedClientAsync(workerEmail, "Password123!", "Worker Summary", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync("/api/worker/attendance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerAttendanceSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalDays);
        Assert.Equal(1, summary.PresentDays);
        Assert.Equal(1, summary.AbsentDays);
        Assert.Equal(50.0m, summary.AttendancePercentage);
    }

    [Fact]
    public async Task AttendanceFilteringByDateWorks()
    {
        // Arrange
        var (_, jobId, assignmentId, _, _) = await SeedAssignmentAsync("farmer.datefilter@test.com");
        var farmerClient = await GetAuthenticatedClientAsync("farmer.datefilter@test.com", "Password123!", "Farmer DateFilter", Roles.Farmer);

        var date1 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
        var date2 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", new SaveJobAttendanceRequest(date1, new List<MarkAttendanceItemRequest> { new(assignmentId, AttendanceStatus.Present) }));
        await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", new SaveJobAttendanceRequest(date2, new List<MarkAttendanceItemRequest> { new(assignmentId, AttendanceStatus.Absent) }));

        // Act: Filter by date1
        var response = await farmerClient.GetAsync($"/api/farmer/jobs/{jobId}/attendance?date={date1:yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<FarmerAttendanceResponse>>(_jsonOptions);
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(date1, list[0].Date);
    }
}
