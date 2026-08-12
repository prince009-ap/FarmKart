using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Application.Options;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class FarmerRegistrationTests
{
    private (ServiceProvider Provider, string DbName) SetupServiceProvider()
    {
        var dbName = $"FarmKartDb_FarmerRegTest_{Guid.NewGuid()}";
        var services = new ServiceCollection();

        services.AddDbContext<FarmKartDbContext>(options =>
            options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True"));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<FarmKartDbContext>();

        services.Configure<JwtOptions>(options =>
        {
            options.Secret = "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!";
            options.Issuer = "FarmKart";
            options.Audience = "FarmKartUsers";
            options.ExpiryMinutes = 60;
        });

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddLogging();
        services.AddScoped<AuthService>();

        return (services.BuildServiceProvider(), dbName);
    }

    [Fact]
    public async Task RegisterFarmer_Successful_CreatesUserAndProfile_WithRole()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            // Create DB
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Seed roles (needed for AddToRoleAsync)
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));
            }

            var request = new FarmerRegisterRequest(
                FullName: "John Doe",
                Email: "farmer.john@example.com",
                Password: "SecurePassword123!",
                Phone: "1234567890",
                ProfileImageUrl: "http://example.com/image.jpg",
                Address: "123 Farm Road",
                FarmName: "Happy Farm",
                FarmSize: 10.5m,
                FarmLocation: "Near Valley"
            );

            // Act
            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var response = await authService.RegisterFarmerAsync(request);

            // Assert response details are safe (no password/JWT/cookies)
            Assert.NotNull(response);
            Assert.Equal(request.FullName, response.FullName);
            Assert.Equal(request.Email, response.Email);
            Assert.Equal(Roles.Farmer, response.Role);
            Assert.True(response.UserId != Guid.Empty);
            Assert.Equal("Farmer registered successfully.", response.Message);

            // Validate in Database
            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            Assert.NotNull(user);
            Assert.Equal(user.Id, response.UserId);

            // Check Role assignment
            var userManager = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var isInRole = await userManager.IsInRoleAsync(user, Roles.Farmer);
            Assert.True(isInRole);

            // Check Profile Creation
            var profile = await db.FarmerProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(profile);
            Assert.Equal(request.FullName, profile.FullName);
            Assert.Equal(request.Phone, profile.Phone);
            Assert.Equal(request.FarmName, profile.FarmName);
            Assert.Equal(request.FarmSize, profile.FarmSize);
            Assert.Equal(request.Address, profile.AddressInfo.AddressLine);
            Assert.Equal(string.Empty, profile.AddressInfo.City);
            Assert.Equal(string.Empty, profile.AddressInfo.State);
            Assert.Equal(string.Empty, profile.AddressInfo.Pincode);
            Assert.Null(profile.AddressInfo.Latitude);
            Assert.Null(profile.AddressInfo.Longitude);

            // Ensure no password hashes/details exist in FarmerProfile
            var type = typeof(FarmerProfile);
            Assert.Null(type.GetProperty("Password"));
            Assert.Null(type.GetProperty("PasswordHash"));
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));

                // Pre-register user with same email
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var existingUser = new ApplicationUser { UserName = "duplicate@example.com", Email = "duplicate@example.com" };
                await userManager.CreateAsync(existingUser, "Password123!");
            }

            var request = new FarmerRegisterRequest(
                FullName: "Duplicate Guy",
                Email: "duplicate@example.com",
                Password: "Password123!",
                Phone: "0987654321",
                ProfileImageUrl: null,
                Address: "Addr",
                FarmName: "Farm",
                FarmSize: 1.0m,
                FarmLocation: null
            );

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

            // Act & Assert
            await Assert.ThrowsAsync<DuplicateEmailException>(() => authService.RegisterFarmerAsync(request));
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_WeakPassword_ThrowsRegistrationFailedException()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));
            }

            var request = new FarmerRegisterRequest(
                FullName: "Test Farmer",
                Email: "weak.password@example.com",
                Password: "123", // Weak password (fails Identity password policies)
                Phone: "1234567890",
                ProfileImageUrl: null,
                Address: "Addr",
                FarmName: "Farm",
                FarmSize: 1.0m,
                FarmLocation: null
            );

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

            // Act & Assert
            await Assert.ThrowsAsync<RegistrationFailedException>(() => authService.RegisterFarmerAsync(request));
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_DatabaseFailure_TransactionRollsBack()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Note: We do NOT seed the "Farmer" role.
                // This will cause RoleManager.AddToRoleAsync to fail inside AuthService, triggering transaction rollback.
            }

            var request = new FarmerRegisterRequest(
                FullName: "Rollback Test",
                Email: "rollback@example.com",
                Password: "Password123!",
                Phone: "1234567890",
                ProfileImageUrl: null,
                Address: "Addr",
                FarmName: "Farm",
                FarmSize: 1.0m,
                FarmLocation: null
            );

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

            // Act & Assert (Should throw RegistrationFailedException because of role assignment failure)
            await Assert.ThrowsAsync<RegistrationFailedException>(() => authService.RegisterFarmerAsync(request));

            // Verify user was NOT created in DB (Transaction rolled back)
            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            Assert.Null(user);

            var profile = await db.FarmerProfiles.SingleOrDefaultAsync(p => p.FullName == request.FullName);
            Assert.Null(profile);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
