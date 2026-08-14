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

public class WorkerEarningsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerEarningsTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerEarningsTest_{Guid.NewGuid()}";
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
    public async Task WorkerCanRetrieveOwnEarnings()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.earningsget@test.com", "Password123!", "Worker EarningsGet", Roles.Worker);

        // Act
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var earnings = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(earnings);
        Assert.NotNull(earnings.EarningsHistory);
    }

    [Fact]
    public async Task WorkerCannotRetrieveAnotherWorkerEarnings()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.earningsA@test.com", "Password123!", "Worker A", Roles.Worker);
        await SetupTestUserAsync("worker.earningsB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Act: Worker A gets own earnings
        var response = await workerAClient.GetAsync("/api/worker/earnings");

        // Assert: Worker A gets 0 earnings
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var earnings = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(earnings);
        Assert.Equal(0, earnings.TotalEarnings);
    }

    [Fact]
    public async Task CompletedValidAssignmentsContributeToEarnings()
    {
        // Arrange
        await SetupTestUserAsync("farmer.earningscomp@test.com", "Password123!", "Farmer Comp", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.earningscomp@test.com", "Password123!", "Worker Comp", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.earningscomp@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.earningscomp@test.com");
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
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert: 6 days * ₹600 = ₹3,600
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(3600, summary.TotalEarnings);
        Assert.Equal(1, summary.CompletedJobsCount);
    }

    [Fact]
    public async Task UncompletedAssignmentsDoNotContribute()
    {
        // Arrange
        await SetupTestUserAsync("farmer.uncomp@test.com", "Password123!", "Farmer Uncomp", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.uncomp@test.com", "Password123!", "Worker Uncomp", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.uncomp@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.uncomp@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var futureStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
            var futureEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Future Job",
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
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert: 0 earnings for future pending assignment with no attendance
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalEarnings);
    }

    [Fact]
    public async Task PresentAttendanceContributes100Percent()
    {
        // Arrange
        await SetupTestUserAsync("farmer.presentatt@test.com", "Password123!", "Farmer Present", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.presentatt@test.com", "Password123!", "Worker Present", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.presentatt@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.presentatt@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Irrigation Work",
                Description = "Irrigation",
                WorkCategory = "Irrigation",
                WorkersRequired = 1,
                WagePerDay = 500,
                StartDate = today.AddDays(-2),
                EndDate = today.AddDays(2),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = AssignmentStatus.Active
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            db.Attendances.Add(new Attendance
            {
                WorkerAssignmentId = assignment.Id,
                Date = today.AddDays(-1),
                Status = AttendanceStatus.Present,
                TotalHours = 8
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert: 1 Present day * ₹500 = ₹500
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(500, summary.TotalEarnings);
    }

    [Fact]
    public async Task HalfDayAttendanceContributes50Percent()
    {
        // Arrange
        await SetupTestUserAsync("farmer.halfday@test.com", "Password123!", "Farmer HalfDay", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.halfday@test.com", "Password123!", "Worker HalfDay", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.halfday@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.halfday@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Half Day Job",
                Description = "Planting",
                WorkCategory = "Planting",
                WorkersRequired = 1,
                WagePerDay = 600,
                StartDate = today.AddDays(-2),
                EndDate = today.AddDays(2),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = AssignmentStatus.Active
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            db.Attendances.Add(new Attendance
            {
                WorkerAssignmentId = assignment.Id,
                Date = today.AddDays(-1),
                Status = AttendanceStatus.HalfDay,
                TotalHours = 4
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert: 1 HalfDay * ₹600 * 50% = ₹300
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(300, summary.TotalEarnings);
    }

    [Fact]
    public async Task AbsentAttendanceDoesNotContribute()
    {
        // Arrange
        await SetupTestUserAsync("farmer.absent@test.com", "Password123!", "Farmer Absent", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.absent@test.com", "Password123!", "Worker Absent", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.absent@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.absent@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Absent Job",
                Description = "Maintenance",
                WorkCategory = "Maintenance",
                WorkersRequired = 1,
                WagePerDay = 500,
                StartDate = today.AddDays(-2),
                EndDate = today.AddDays(2),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = AssignmentStatus.Active
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            db.Attendances.Add(new Attendance
            {
                WorkerAssignmentId = assignment.Id,
                Date = today.AddDays(-1),
                Status = AttendanceStatus.Absent,
                TotalHours = 0
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert: 0 earnings for Absent day
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalEarnings);
    }

    [Fact]
    public async Task EarningsUseJobDailyWage()
    {
        // Arrange
        await SetupTestUserAsync("farmer.dailywage@test.com", "Password123!", "Farmer DailyWage", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.dailywage@test.com", "Password123!", "Worker DailyWage", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.dailywage@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.dailywage@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "High Wage Job",
                Description = "Specialized harvesting",
                WorkCategory = "Harvesting",
                WorkersRequired = 1,
                WagePerDay = 850,
                StartDate = today.AddDays(-2),
                EndDate = today.AddDays(2),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();

            var assignment = new WorkerAssignment
            {
                JobId = job.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = AssignmentStatus.Active
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();

            db.Attendances.Add(new Attendance
            {
                WorkerAssignmentId = assignment.Id,
                Date = today.AddDays(-1),
                Status = AttendanceStatus.Present,
                TotalHours = 8
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert: 1 Present * ₹850 = ₹850
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Single(summary.EarningsHistory);
        Assert.Equal(850, summary.EarningsHistory[0].DailyWage);
        Assert.Equal(850, summary.EarningsHistory[0].TotalEarned);
    }

    [Fact]
    public async Task CompletedJobCountIsCorrect()
    {
        // Arrange
        await SetupTestUserAsync("farmer.jobcount@test.com", "Password123!", "Farmer JobCount", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.jobcount@test.com", "Password123!", "Worker JobCount", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.jobcount@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.jobcount@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var pastStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20));
            var pastEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15));

            var job1 = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Job 1",
                Description = "Desc 1",
                WorkCategory = "Sowing",
                WorkersRequired = 1,
                WagePerDay = 400,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = JobStatus.Completed
            };
            db.Jobs.Add(job1);
            await db.SaveChangesAsync();

            db.WorkerAssignments.Add(new WorkerAssignment
            {
                JobId = job1.Id,
                WorkerProfileId = workerProfile.Id,
                StartDate = pastStart,
                EndDate = pastEnd,
                Status = AssignmentStatus.Completed
            });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/earnings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WorkerEarningsSummaryResponse>(_jsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.CompletedJobsCount);
    }

    [Fact]
    public async Task UnauthenticatedAccessIsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/earnings");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotAccessWorkerEarningsEndpoint()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.noearnings@test.com", "Password123!", "Farmer NoEarnings", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync("/api/worker/earnings");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotAccessWorkerEarningsEndpoint()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.noearnings@test.com", "Password123!", "Customer NoEarnings", Roles.Customer);

        // Act
        var response = await customerClient.GetAsync("/api/worker/earnings");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
