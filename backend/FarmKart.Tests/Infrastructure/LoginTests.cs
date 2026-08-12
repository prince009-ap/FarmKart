using FarmKart.Application.DTOs;
using FarmKart.Application.Exceptions;
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

public class LoginTests
{
    private (ServiceProvider Provider, string DbName) SetupServiceProvider()
    {
        var dbName = $"FarmKartDb_LoginTest_{Guid.NewGuid()}";
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

        services.AddLogging();
        services.AddScoped<AuthService>();

        return (services.BuildServiceProvider(), dbName);
    }

    [Fact]
    public async Task Login_Farmer_WithValidCredentials_Succeeds()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Seed roles
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));

                // Pre-register Farmer
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var registerRequest = new FarmerRegisterRequest(
                    FullName: "Farmer John",
                    Email: "farmer.john@test.com",
                    Password: "SecurePassword123!",
                    Phone: "1234567890",
                    ProfileImageUrl: null,
                    Address: "123 Farm Road",
                    FarmName: "Happy Farm",
                    FarmSize: 10.5m,
                    FarmLocation: "Near Valley"
                );
                await authService.RegisterFarmerAsync(registerRequest);
            }

            // Act
            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("farmer.john@test.com", "SecurePassword123!");
            var response = await authService2.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("farmer.john@test.com", response.Email);
            Assert.Equal("Farmer John", response.FullName);
            Assert.Equal(Roles.Farmer, response.Role);
            Assert.True(response.UserId != Guid.Empty);
            Assert.Equal("Login successful.", response.Message);

            // Verify no sensitive properties in output DTO properties
            var type = typeof(LoginResponse);
            Assert.Null(type.GetProperty("Password"));
            Assert.Null(type.GetProperty("PasswordHash"));
            Assert.Null(type.GetProperty("Token"));
            Assert.Null(type.GetProperty("Jwt"));
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Login_Worker_WithValidCredentials_Succeeds()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Seed roles
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Worker));

                // Pre-register Worker
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var registerRequest = new WorkerRegisterRequest(
                    FullName: "Worker Jane",
                    Email: "worker.jane@test.com",
                    Password: "SecurePassword123!",
                    Phone: "0987654321",
                    ProfileImageUrl: null,
                    Address: "456 Field Lane",
                    ExperienceYears: 5,
                    ExpectedDailyWage: 120.00m
                );
                await authService.RegisterWorkerAsync(registerRequest);
            }

            // Act
            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("worker.jane@test.com", "SecurePassword123!");
            var response = await authService2.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("worker.jane@test.com", response.Email);
            Assert.Equal("Worker Jane", response.FullName);
            Assert.Equal(Roles.Worker, response.Role);
            Assert.True(response.UserId != Guid.Empty);
            Assert.Equal("Login successful.", response.Message);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Login_Customer_WithValidCredentials_Succeeds()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Seed roles
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

                // Pre-register Customer
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var registerRequest = new CustomerRegisterRequest(
                    FullName: "Customer Alice",
                    Email: "customer.alice@test.com",
                    Password: "SecurePassword123!",
                    Phone: "1112223333",
                    ProfileImageUrl: null,
                    Address: "789 Main Street"
                );
                await authService.RegisterCustomerAsync(registerRequest);
            }

            // Act
            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("customer.alice@test.com", "SecurePassword123!");
            var response = await authService2.LoginAsync(loginRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("customer.alice@test.com", response.Email);
            Assert.Equal("Customer Alice", response.FullName);
            Assert.Equal(Roles.Customer, response.Role);
            Assert.True(response.UserId != Guid.Empty);
            Assert.Equal("Login successful.", response.Message);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Login_UnknownEmail_ThrowsInvalidCredentialsException()
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

            // Act
            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("unknown@test.com", "SecurePassword123!");

            // Assert
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => authService2.LoginAsync(loginRequest));
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Login_IncorrectPassword_ThrowsInvalidCredentialsException()
    {
        var (provider, dbName) = SetupServiceProvider();
        try
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Seed roles
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

                // Pre-register Customer
                var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                var registerRequest = new CustomerRegisterRequest(
                    FullName: "Customer Alice",
                    Email: "customer.alice@test.com",
                    Password: "SecurePassword123!",
                    Phone: "1112223333",
                    ProfileImageUrl: null,
                    Address: "789 Main Street"
                );
                await authService.RegisterCustomerAsync(registerRequest);
            }

            // Act
            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("customer.alice@test.com", "WrongPassword123!");

            // Assert
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => authService2.LoginAsync(loginRequest));
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
