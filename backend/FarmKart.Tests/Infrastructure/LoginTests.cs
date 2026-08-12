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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class LoginTests
{
    private const string TestSecret = "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!";
    private const string TestIssuer = "FarmKart";
    private const string TestAudience = "FarmKartUsers";
    private const int TestExpiryMinutes = 60;

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

        // Register JWT configuration and services
        services.Configure<JwtOptions>(options =>
        {
            options.Secret = TestSecret;
            options.Issuer = TestIssuer;
            options.Audience = TestAudience;
            options.ExpiryMinutes = TestExpiryMinutes;
        });

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddLogging();
        services.AddScoped<AuthService>();

        return (services.BuildServiceProvider(), dbName);
    }

    [Fact]
    public async Task Login_Farmer_WithValidCredentials_GeneratesValidJwtAndExpiresAt()
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

            // Assert response fields (returns LoginResult at service level)
            Assert.NotNull(response);
            Assert.Equal("farmer.john@test.com", response.Email);
            Assert.Equal("Farmer John", response.FullName);
            Assert.Equal(Roles.Farmer, response.Role);
            Assert.True(response.UserId != Guid.Empty);
            Assert.False(string.IsNullOrWhiteSpace(response.Token));
            Assert.True(response.ExpiresAt > DateTime.UtcNow);
            Assert.Equal("Login successful.", response.Message);

            // Verify JWT payload, claims, and signature
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = TestIssuer,
                ValidAudience = TestAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret))
            };

            var principal = tokenHandler.ValidateToken(response.Token, validationParameters, out var validatedToken);
            Assert.NotNull(validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            Assert.Equal(response.UserId.ToString(), jwtToken.Claims.First(c => c.Type == "sub" || c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier).Value);
            Assert.Equal("farmer.john@test.com", jwtToken.Claims.First(c => c.Type == "email" || c.Type == ClaimTypes.Email).Value);
            Assert.Equal(Roles.Farmer, jwtToken.Claims.First(c => c.Type == "role" || c.Type == ClaimTypes.Role).Value);
            Assert.Equal(TestIssuer, jwtToken.Issuer);
            Assert.Equal(TestAudience, jwtToken.Audiences.First());

            // Ensure password details or secrets are not in response or JWT
            Assert.DoesNotContain("Password", response.Token);
            Assert.DoesNotContain(TestSecret, response.Token);

            // Assert LoginResponse DTO does NOT expose token or password details
            var type = typeof(LoginResponse);
            Assert.Null(type.GetProperty("Token"));
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
    public async Task Login_Worker_WithValidCredentials_GeneratesValidJwtAndExpiresAt()
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
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Worker));

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
            Assert.Equal(Roles.Worker, response.Role);
            Assert.False(string.IsNullOrWhiteSpace(response.Token));

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(response.Token);
            Assert.Equal(Roles.Worker, jwtToken.Claims.First(c => c.Type == "role" || c.Type == ClaimTypes.Role).Value);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Login_Customer_WithValidCredentials_GeneratesValidJwtAndExpiresAt()
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
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

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
            Assert.Equal(Roles.Customer, response.Role);
            Assert.False(string.IsNullOrWhiteSpace(response.Token));

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(response.Token);
            Assert.Equal(Roles.Customer, jwtToken.Claims.First(c => c.Type == "role" || c.Type == ClaimTypes.Role).Value);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Login_UnknownEmail_DoesNotGenerateJwt_ThrowsInvalidCredentialsException()
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

            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("unknown@test.com", "SecurePassword123!");

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
    public async Task Login_IncorrectPassword_DoesNotGenerateJwt_ThrowsInvalidCredentialsException()
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
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Customer));

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

            using var scope2 = provider.CreateScope();
            var authService2 = scope2.ServiceProvider.GetRequiredService<AuthService>();
            var loginRequest = new LoginRequest("customer.alice@test.com", "WrongPassword123!");

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
