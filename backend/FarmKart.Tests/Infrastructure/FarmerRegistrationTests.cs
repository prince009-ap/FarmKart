using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
using FarmKart.Application.Options;
using FarmKart.Domain.Common;
using FarmKart.Domain.Entities;
using FarmKart.Domain.Enums;
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

    private static FarmerRegisterRequest CreateValidRequest(
        string email = "farmer.john@example.com",
        string? farmName = "Happy Farm") =>
        new(
            FullName: "John Doe",
            Email: email,
            Password: "SecurePassword123!",
            Phone: "1234567890",
            ProfileImageUrl: "http://example.com/image.jpg",
            Address: "123 Farm Road",
            FarmName: farmName,
            FarmSize: 10.5m,
            FarmSizeUnit: FarmSizeUnit.Vigha,
            FarmLocation: "Near Valley"
        );

    [Fact]
    public async Task RegisterFarmer_Successful_CreatesUserAndProfile_WithRole()
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

            var request = CreateValidRequest();

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var response = await authService.RegisterFarmerAsync(request);

            Assert.NotNull(response);
            Assert.Equal(request.FullName, response.FullName);
            Assert.Equal(request.Email, response.Email);
            Assert.Equal(Roles.Farmer, response.Role);
            Assert.True(response.UserId != Guid.Empty);
            Assert.Equal("Farmer registered successfully.", response.Message);

            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
            Assert.NotNull(user);
            Assert.Equal(user.Id, response.UserId);

            var userManager = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var isInRole = await userManager.IsInRoleAsync(user, Roles.Farmer);
            Assert.True(isInRole);

            var profile = await db.FarmerProfiles.SingleOrDefaultAsync(p => p.UserId == user.Id);
            Assert.NotNull(profile);
            Assert.Equal(request.FullName, profile.FullName);
            Assert.Equal(request.Phone, profile.Phone);
            Assert.Equal(request.FarmName, profile.FarmName);
            Assert.Equal(request.FarmSize, profile.FarmSize);
            Assert.Equal(FarmSizeUnit.Vigha, profile.FarmSizeUnit);
            Assert.Equal(request.Address, profile.AddressInfo.AddressLine);
            Assert.Equal(string.Empty, profile.AddressInfo.City);
            Assert.Equal(string.Empty, profile.AddressInfo.State);
            Assert.Equal(string.Empty, profile.AddressInfo.Pincode);
            Assert.Null(profile.AddressInfo.Latitude);
            Assert.Null(profile.AddressInfo.Longitude);

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
    public async Task RegisterFarmer_WithVighaUnit_StoresFarmSizeUnitAsVigha()
    {
        var (provider, _) = SetupServiceProvider();
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

            var request = CreateValidRequest(farmName: "Green Valley Farm", email: "vigha.farmer@example.com");
            request = request with { FarmSize = 5m, FarmSizeUnit = FarmSizeUnit.Vigha };

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();
            await authService.RegisterFarmerAsync(request);

            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var profile = await db.FarmerProfiles.SingleAsync(p => p.FarmName == "Green Valley Farm");
            Assert.Equal(5m, profile.FarmSize);
            Assert.Equal(FarmSizeUnit.Vigha, profile.FarmSizeUnit);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_WithOptionalFarmName_AllowsNullFarmName()
    {
        var (provider, _) = SetupServiceProvider();
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

            var request = CreateValidRequest(email: "no.farmname@example.com", farmName: null);

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();
            await authService.RegisterFarmerAsync(request);

            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var profile = await db.FarmerProfiles.SingleAsync(p => p.FullName == request.FullName);
            Assert.Null(profile.FarmName);
            Assert.Equal(FarmSizeUnit.Vigha, profile.FarmSizeUnit);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_WithAcreUnit_StoresFarmSizeUnitAsAcre()
    {
        var (provider, _) = SetupServiceProvider();
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

            var request = CreateValidRequest(email: "acre.farmer@example.com") with
            {
                FarmSize = 12m,
                FarmSizeUnit = FarmSizeUnit.Acre
            };

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();
            await authService.RegisterFarmerAsync(request);

            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var profile = await db.FarmerProfiles.SingleAsync(p => p.FullName == request.FullName);
            Assert.Equal(FarmSizeUnit.Acre, profile.FarmSizeUnit);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_WithHectareUnit_StoresFarmSizeUnitAsHectare()
    {
        var (provider, _) = SetupServiceProvider();
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

            var request = CreateValidRequest(email: "hectare.farmer@example.com") with
            {
                FarmSize = 8m,
                FarmSizeUnit = FarmSizeUnit.Hectare
            };

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();
            await authService.RegisterFarmerAsync(request);

            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var profile = await db.FarmerProfiles.SingleAsync(p => p.FullName == request.FullName);
            Assert.Equal(FarmSizeUnit.Hectare, profile.FarmSizeUnit);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RegisterFarmer_InvalidFarmSizeUnit_ThrowsRegistrationFailedException()
    {
        var (provider, _) = SetupServiceProvider();
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

            var request = CreateValidRequest(email: "invalid.unit@example.com") with
            {
                FarmSizeUnit = (FarmSizeUnit)999
            };

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

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
    public async Task LegacyFarmerProfile_WithoutFarmSizeUnit_RemainsNull()
    {
        var (provider, _) = SetupServiceProvider();
        try
        {
            var legacyUserId = Guid.NewGuid();

            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                context.Users.Add(new ApplicationUser
                {
                    Id = legacyUserId,
                    UserName = "legacy.farmer@example.com",
                    Email = "legacy.farmer@example.com",
                    NormalizedEmail = "LEGACY.FARMER@EXAMPLE.COM",
                    NormalizedUserName = "LEGACY.FARMER@EXAMPLE.COM"
                });

                context.FarmerProfiles.Add(new FarmerProfile
                {
                    UserId = legacyUserId,
                    FullName = "Legacy Farmer",
                    Phone = "1234567890",
                    FarmSize = 10.5m,
                    FarmSizeUnit = null,
                    AddressInfo = new FarmKart.Domain.ValueObjects.AddressInfo
                    {
                        AddressLine = "Legacy Address"
                    }
                });
                await context.SaveChangesAsync();
            }

            using var scope2 = provider.CreateScope();
            var db = scope2.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var profile = await db.FarmerProfiles.SingleAsync(p => p.FullName == "Legacy Farmer");

            Assert.Equal(10.5m, profile.FarmSize);
            Assert.Null(profile.FarmSizeUnit);
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

                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var existingUser = new ApplicationUser { UserName = "duplicate@example.com", Email = "duplicate@example.com" };
                await userManager.CreateAsync(existingUser, "Password123!");
            }

            var request = CreateValidRequest(email: "duplicate@example.com", farmName: "Farm");

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

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

            var request = CreateValidRequest(email: "weak.password@example.com", farmName: "Farm") with
            {
                Password = "123"
            };

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

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
            }

            var request = CreateValidRequest(email: "rollback@example.com", farmName: "Farm");

            using var scope2 = provider.CreateScope();
            var authService = scope2.ServiceProvider.GetRequiredService<AuthService>();

            await Assert.ThrowsAsync<RegistrationFailedException>(() => authService.RegisterFarmerAsync(request));

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
