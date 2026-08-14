using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.Abstractions.Farmer;
using FarmKart.Application.Abstractions.Notification;
using FarmKart.Application.Abstractions.Worker;
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

public class WorkerNotificationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public WorkerNotificationTests(WebApplicationFactory<Program> factory)
    {
        _dbName = $"FarmKartDb_WorkerNotifTest_{Guid.NewGuid()}";
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
    public async Task WorkerCanRetrieveOwnNotifications()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.notifget@test.com", "Password123!", "Worker NotifGet", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "worker.notifget@test.com");
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifService.CreateNotificationAsync(user.Id.ToString(), "Test Title", "Test Message", NotificationType.General);
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/notifications");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notifs = await response.Content.ReadFromJsonAsync<List<WorkerNotificationResponse>>(_jsonOptions);
        Assert.NotNull(notifs);
        Assert.Single(notifs);
        Assert.Equal("Test Title", notifs[0].Title);
        Assert.Equal("Test Message", notifs[0].Message);
    }

    [Fact]
    public async Task WorkerCanRetrieveUnreadCount()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.unreadcount@test.com", "Password123!", "Worker UnreadCount", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "worker.unreadcount@test.com");
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifService.CreateNotificationAsync(user.Id.ToString(), "Notif 1", "Msg 1", NotificationType.General);
            await notifService.CreateNotificationAsync(user.Id.ToString(), "Notif 2", "Msg 2", NotificationType.General);
        }

        // Act
        var response = await workerClient.GetAsync("/api/worker/notifications/unread-count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unread = await response.Content.ReadFromJsonAsync<UnreadNotificationCountResponse>(_jsonOptions);
        Assert.NotNull(unread);
        Assert.Equal(2, unread.UnreadCount);
    }

    [Fact]
    public async Task WorkerCanMarkOwnNotificationAsRead()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.markread@test.com", "Password123!", "Worker MarkRead", Roles.Worker);
        Guid notifId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "worker.markread@test.com");
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifService.CreateNotificationAsync(user.Id.ToString(), "Notif Read", "Msg", NotificationType.General);
            var n = await db.Notifications.SingleAsync(x => x.RecipientUserId == user.Id.ToString());
            notifId = n.Id;
        }

        // Act
        var response = await workerClient.PutAsync($"/api/worker/notifications/{notifId}/read", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notif = await response.Content.ReadFromJsonAsync<WorkerNotificationResponse>(_jsonOptions);
        Assert.NotNull(notif);
        Assert.True(notif.IsRead);
    }

    [Fact]
    public async Task WorkerCanMarkAllOwnNotificationsAsRead()
    {
        // Arrange
        var workerClient = await GetAuthenticatedClientAsync("worker.markallread@test.com", "Password123!", "Worker MarkAllRead", Roles.Worker);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "worker.markallread@test.com");
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifService.CreateNotificationAsync(user.Id.ToString(), "Notif A", "Msg A", NotificationType.General);
            await notifService.CreateNotificationAsync(user.Id.ToString(), "Notif B", "Msg B", NotificationType.General);
        }

        // Act
        var response = await workerClient.PutAsync("/api/worker/notifications/read-all", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var countResp = await workerClient.GetAsync("/api/worker/notifications/unread-count");
        var unread = await countResp.Content.ReadFromJsonAsync<UnreadNotificationCountResponse>(_jsonOptions);
        Assert.NotNull(unread);
        Assert.Equal(0, unread.UnreadCount);
    }

    [Fact]
    public async Task WorkerCannotAccessAnotherWorkerNotification()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.notifA@test.com", "Password123!", "Worker A", Roles.Worker);
        await SetupTestUserAsync("worker.notifB@test.com", "Password123!", "Worker B", Roles.Worker);

        // Create notification for Worker B only
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userB = await db.Users.SingleAsync(u => u.Email == "worker.notifB@test.com");
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifService.CreateNotificationAsync(userB.Id.ToString(), "Private B", "Msg B", NotificationType.General);
        }

        // Act: Worker A gets own notifications
        var response = await workerAClient.GetAsync("/api/worker/notifications");

        // Assert: Worker A sees 0 notifications
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notifs = await response.Content.ReadFromJsonAsync<List<WorkerNotificationResponse>>(_jsonOptions);
        Assert.NotNull(notifs);
        Assert.Empty(notifs);
    }

    [Fact]
    public async Task WorkerCannotModifyAnotherWorkerNotification()
    {
        // Arrange: Worker A and Worker B
        var workerAClient = await GetAuthenticatedClientAsync("worker.modA@test.com", "Password123!", "Worker A", Roles.Worker);
        await SetupTestUserAsync("worker.modB@test.com", "Password123!", "Worker B", Roles.Worker);
        Guid notifBId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var userB = await db.Users.SingleAsync(u => u.Email == "worker.modB@test.com");
            var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notifService.CreateNotificationAsync(userB.Id.ToString(), "Private B", "Msg B", NotificationType.General);
            var nB = await db.Notifications.SingleAsync(x => x.RecipientUserId == userB.Id.ToString());
            notifBId = nB.Id;
        }

        // Act: Worker A tries to mark B's notification as read
        var response = await workerAClient.PutAsync($"/api/worker/notifications/{notifBId}/read", null);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedUserIsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/worker/notifications");

        // Assert: 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FarmerCannotAccessWorkerNotificationEndpoint()
    {
        // Arrange
        var farmerClient = await GetAuthenticatedClientAsync("farmer.nonotif@test.com", "Password123!", "Farmer NoNotif", Roles.Farmer);

        // Act
        var response = await farmerClient.GetAsync("/api/worker/notifications");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerCannotAccessWorkerNotificationEndpoint()
    {
        // Arrange
        var customerClient = await GetAuthenticatedClientAsync("customer.nonotif@test.com", "Password123!", "Customer NoNotif", Roles.Customer);

        // Act
        var response = await customerClient.GetAsync("/api/worker/notifications");

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApplicationAcceptanceCreatesWorkerNotification()
    {
        // Arrange: Farmer, Worker, Job, Application
        var farmerClient = await GetAuthenticatedClientAsync("farmer.appaccept@test.com", "Password123!", "Farmer Accept", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.appaccept@test.com", "Password123!", "Worker Accept", Roles.Worker);

        Guid jobId;
        Guid applicationId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.appaccept@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Harvesting Job Accept Test",
                Description = "Harvesting wheat",
                WorkCategory = "Harvesting",
                WorkersRequired = 2,
                WagePerDay = 500,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.appaccept@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var application = new JobApplication
            {
                JobId = jobId,
                WorkerProfileId = workerProfile.Id,
                Status = ApplicationStatus.Pending
            };
            db.JobApplications.Add(application);
            await db.SaveChangesAsync();
            applicationId = application.Id;
        }

        // Act: Farmer accepts application
        var acceptResp = await farmerClient.PostAsync($"/api/farmer/applications/{applicationId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // Assert: Worker has notification
        var notifResp = await workerClient.GetAsync("/api/worker/notifications");
        Assert.Equal(HttpStatusCode.OK, notifResp.StatusCode);
        var notifs = await notifResp.Content.ReadFromJsonAsync<List<WorkerNotificationResponse>>(_jsonOptions);
        Assert.NotNull(notifs);
        Assert.Contains(notifs, n => n.Title.Contains("Application Accepted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AssignmentCreationCreatesWorkerNotification()
    {
        // Arrange: Farmer, Worker, Job, Application
        var farmerClient = await GetAuthenticatedClientAsync("farmer.assigntest@test.com", "Password123!", "Farmer Assign", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.assigntest@test.com", "Password123!", "Worker Assign", Roles.Worker);

        Guid jobId;
        Guid applicationId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.assigntest@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Assignment Job Test",
                Description = "Sowing crops",
                WorkCategory = "Sowing",
                WorkersRequired = 2,
                WagePerDay = 450,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.assigntest@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var application = new JobApplication
            {
                JobId = jobId,
                WorkerProfileId = workerProfile.Id,
                Status = ApplicationStatus.Pending
            };
            db.JobApplications.Add(application);
            await db.SaveChangesAsync();
            applicationId = application.Id;
        }

        // Act: Farmer accepts application (which creates assignment)
        var acceptResp = await farmerClient.PostAsync($"/api/farmer/applications/{applicationId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        // Assert: Worker receives Assignment Created notification
        var notifResp = await workerClient.GetAsync("/api/worker/notifications");
        var notifs = await notifResp.Content.ReadFromJsonAsync<List<WorkerNotificationResponse>>(_jsonOptions);
        Assert.NotNull(notifs);
        Assert.Contains(notifs, n => n.Title.Contains("Assignment Created", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AttendanceUpdateCreatesWorkerNotification()
    {
        // Arrange: Farmer, Worker, Job, Assignment
        var farmerClient = await GetAuthenticatedClientAsync("farmer.atttest@test.com", "Password123!", "Farmer Att", Roles.Farmer);
        var workerClient = await GetAuthenticatedClientAsync("worker.atttest@test.com", "Password123!", "Worker Att", Roles.Worker);

        Guid jobId;
        Guid assignmentId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var farmerUser = await db.Users.SingleAsync(u => u.Email == "farmer.atttest@test.com");
            var farmerProfile = await db.FarmerProfiles.SingleAsync(f => f.UserId == farmerUser.Id);

            var job = new Job
            {
                FarmerProfileId = farmerProfile.Id,
                Title = "Attendance Job Test",
                Description = "Irrigation work",
                WorkCategory = "Irrigation",
                WorkersRequired = 2,
                WagePerDay = 400,
                StartDate = today.AddDays(-2),
                EndDate = today.AddDays(5),
                Status = JobStatus.Open
            };
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;

            var workerUser = await db.Users.SingleAsync(u => u.Email == "worker.atttest@test.com");
            var workerProfile = await db.WorkerProfiles.SingleAsync(w => w.UserId == workerUser.Id);

            var assignment = new WorkerAssignment
            {
                JobId = jobId,
                WorkerProfileId = workerProfile.Id,
                StartDate = job.StartDate,
                EndDate = job.EndDate,
                Status = AssignmentStatus.Active
            };
            db.WorkerAssignments.Add(assignment);
            await db.SaveChangesAsync();
            assignmentId = assignment.Id;
        }

        // Act: Farmer saves attendance
        var req = new SaveJobAttendanceRequest(
            Date: today,
            Items: new List<MarkAttendanceItemRequest>
            {
                new MarkAttendanceItemRequest(assignmentId, AttendanceStatus.Present, "Worked hard")
            }
        );
        var saveResp = await farmerClient.PostAsJsonAsync($"/api/farmer/jobs/{jobId}/attendance", req);
        Assert.Equal(HttpStatusCode.OK, saveResp.StatusCode);

        // Assert: Worker receives Attendance Updated notification
        var notifResp = await workerClient.GetAsync("/api/worker/notifications");
        var notifs = await notifResp.Content.ReadFromJsonAsync<List<WorkerNotificationResponse>>(_jsonOptions);
        Assert.NotNull(notifs);
        Assert.Contains(notifs, n => n.Title.Contains("Attendance Updated", StringComparison.OrdinalIgnoreCase));
    }
}
