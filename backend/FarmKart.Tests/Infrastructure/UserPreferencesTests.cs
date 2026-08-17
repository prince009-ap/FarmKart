using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class UserPreferencesTests : IDisposable
{
    private readonly FarmKartDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserPreferenceService _service;
    private readonly string _dbName;

    public UserPreferencesTests()
    {
        _dbName = $"FarmKartDb_UserPreferencesTest_{Guid.NewGuid():N}";
        var services = new ServiceCollection();

        services.AddDbContext<FarmKartDbContext>(options =>
            options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True"));

        services.AddLogging();
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<FarmKartDbContext>();

        var provider = services.BuildServiceProvider();
        _db = provider.GetRequiredService<FarmKartDbContext>();
        _db.Database.EnsureCreated();

        _userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        _service = new UserPreferenceService(_db, _userManager);
    }

    [Fact]
    public async Task GetUserPreferenceAsync_NewUser_ReturnsDefaultPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "pref_user1@test.com", Email = "pref_user1@test.com" };
        await _userManager.CreateAsync(user, "Password123");

        // Act
        var res = await _service.GetUserPreferenceAsync(userId);

        // Assert
        Assert.NotNull(res);
        Assert.Equal("light", res.Theme);
        Assert.Equal("en", res.Language);
        Assert.True(res.EmailAlerts);
        Assert.False(res.SmsAlerts);
        Assert.False(res.CompactView);
    }

    [Fact]
    public async Task UpdateUserPreferenceAsync_ValidRequest_UpdatesAndPersistsPreferences()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "pref_user2@test.com", Email = "pref_user2@test.com" };
        await _userManager.CreateAsync(user, "Password123");

        var updateReq = new UpdateUserPreferenceRequest(
            Theme: "dark",
            Language: "hi",
            EmailAlerts: false,
            SmsAlerts: true,
            CompactView: true
        );

        // Act
        var res = await _service.UpdateUserPreferenceAsync(userId, updateReq);

        // Assert
        Assert.NotNull(res);
        Assert.Equal("dark", res.Theme);
        Assert.Equal("hi", res.Language);
        Assert.False(res.EmailAlerts);
        Assert.True(res.SmsAlerts);
        Assert.True(res.CompactView);

        // Verify persistence on fresh read
        var fetched = await _service.GetUserPreferenceAsync(userId);
        Assert.Equal("dark", fetched.Theme);
        Assert.Equal("hi", fetched.Language);
    }

    [Fact]
    public async Task GetAccountSettingsAsync_FarmerUser_ReturnsAccountInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "farmer_sett@test.com", Email = "farmer_sett@test.com", PhoneNumber = "9876543210" };
        await _userManager.CreateAsync(user, "Password123");

        var farmerProfile = new FarmerProfile
        {
            UserId = userId,
            FullName = "Ramesh Farmer",
            Phone = "9876543210"
        };
        _db.FarmerProfiles.Add(farmerProfile);
        await _db.SaveChangesAsync();

        // Act
        var res = await _service.GetAccountSettingsAsync(userId, Roles.Farmer);

        // Assert
        Assert.NotNull(res);
        Assert.Equal(userId, res.UserId);
        Assert.Equal("Ramesh Farmer", res.FullName);
        Assert.Equal("farmer_sett@test.com", res.Email);
        Assert.Equal(Roles.Farmer, res.Role);
        Assert.Equal("9876543210", res.Phone);
    }

    [Fact]
    public async Task UpdateAccountProfileAsync_ValidNameAndPhone_UpdatesProfileAndUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "cust_sett@test.com", Email = "cust_sett@test.com", PhoneNumber = "1111111111" };
        await _userManager.CreateAsync(user, "Password123");

        var customerProfile = new CustomerProfile
        {
            UserId = userId,
            FullName = "Old Name",
            Phone = "1111111111"
        };
        _db.CustomerProfiles.Add(customerProfile);
        await _db.SaveChangesAsync();

        var updateReq = new UpdateAccountProfileRequest("New Name", "9999999999");

        // Act
        var res = await _service.UpdateAccountProfileAsync(userId, Roles.Customer, updateReq);

        // Assert
        Assert.Equal("New Name", res.FullName);
        Assert.Equal("9999999999", res.Phone);

        var updatedUser = await _userManager.FindByIdAsync(userId.ToString());
        Assert.Equal("9999999999", updatedUser!.PhoneNumber);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "pwd_user1@test.com", Email = "pwd_user1@test.com" };
        await _userManager.CreateAsync(user, "OldPassword123");

        var req = new ChangePasswordRequest("WrongPass", "NewPassword123", "NewPassword123");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ChangePasswordAsync(userId, req));
    }

    [Fact]
    public async Task ChangePasswordAsync_MismatchConfirmPassword_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "pwd_user2@test.com", Email = "pwd_user2@test.com" };
        await _userManager.CreateAsync(user, "OldPassword123");

        var req = new ChangePasswordRequest("OldPassword123", "NewPassword123", "Mismatch123");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangePasswordAsync(userId, req));
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidPasswords_SuccessfullyChangesPassword()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new ApplicationUser { Id = userId, UserName = "pwd_user3@test.com", Email = "pwd_user3@test.com" };
        await _userManager.CreateAsync(user, "OldPassword123");

        var req = new ChangePasswordRequest("OldPassword123", "NewPassword123", "NewPassword123");

        // Act
        await _service.ChangePasswordAsync(userId, req);

        // Assert
        var updatedUser = await _userManager.FindByIdAsync(userId.ToString());
        var isValid = await _userManager.CheckPasswordAsync(updatedUser!, "NewPassword123");
        Assert.True(isValid);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
