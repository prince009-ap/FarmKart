using FarmKart.Application.Abstractions.Authentication;
using FarmKart.Application.DTOs;
using FarmKart.Domain.Common;
using FarmKart.Domain.Enums;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class CookieAuthTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public CookieAuthTests(WebApplicationFactory<Program> factory)
    {
        var dbName = $"FarmKartDb_CookieAuthTest_{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Set development configurations
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Secret", "ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!" },
                    { "JwtSettings:Issuer", "FarmKart" },
                    { "JwtSettings:Audience", "FarmKartUsers" },
                    { "JwtSettings:ExpiryMinutes", "60" },
                    { "JwtSettings:CookieName", "FarmKartAuth" },
                    { "JwtSettings:CookieSecure", "false" }, // Development default
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
                    options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True"));

                // Build a temporary service provider to create the database schema before host startup
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
            await authService.RegisterFarmerAsync(new FarmerRegisterRequest(fullName, email, password, "1234567890", null, "123 Farm Road", "Happy Farm", 10.5m, FarmSizeUnit.Vigha, "Near Valley"));
        }
        else if (role == Roles.Worker)
        {
            await authService.RegisterWorkerAsync(new WorkerRegisterRequest(fullName, email, password, "1234567890", null, "123 Worker Road", 2, 100));
        }
        else if (role == Roles.Customer)
        {
            await authService.RegisterCustomerAsync(new CustomerRegisterRequest(fullName, email, password, "1234567890", null, "123 Customer Road"));
        }
    }

    [Fact]
    public async Task Login_WritesJwtToHttpOnlyCookie_WithCorrectAttributes()
    {
        // Arrange
        await SetupTestUserAsync("farmer.cookie@test.com", "SecurePassword123!", "Farmer Cookie", Roles.Farmer);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Act
        var loginRequest = new LoginRequest("farmer.cookie@test.com", "SecurePassword123!");
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert response success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert response JSON has no JWT token or JWT secret
        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Token", rawJson);
        Assert.DoesNotContain("token", rawJson);
        Assert.DoesNotContain("ThisIsADevelopmentSecretKey", rawJson);

        // Assert cookie is set in response headers
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var setCookieHeader = response.Headers.GetValues("Set-Cookie").First();
        
        Assert.Contains("FarmKartAuth=", setCookieHeader);
        Assert.Contains("httponly", setCookieHeader.ToLower());
        Assert.Contains("path=/", setCookieHeader.ToLower());
        
        // Under dev configuration (CookieSecure=false), "secure" is not present
        Assert.DoesNotContain("secure", setCookieHeader.ToLower());
    }

    [Fact]
    public async Task Login_UnderProductionConfiguration_SetsSecureTrueOnCookie()
    {
        // Arrange: Build a client overriding CookieSecure to true
        var prodFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:CookieSecure", "true" } // Production setting
                });
            });
        });

        // Setup test user inside the prod factory context database
        using (var scope = prodFactory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roleManager.RoleExistsAsync(Roles.Farmer))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(Roles.Farmer));
            }

            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            
            // Check if user already exists in DB to prevent duplicates
            var db = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
            var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "farmer.cookie@test.com");
            if (existingUser == null)
            {
                await authService.RegisterFarmerAsync(new FarmerRegisterRequest("Farmer Cookie", "farmer.cookie@test.com", "SecurePassword123!", "1234567890", null, "123 Farm Road", "Happy Farm", 10.5m, FarmSizeUnit.Vigha, "Near Valley"));
            }
        }

        var client = prodFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRequest = new LoginRequest("farmer.cookie@test.com", "SecurePassword123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var setCookieHeader = response.Headers.GetValues("Set-Cookie").First();
        Assert.Contains("secure", setCookieHeader.ToLower());
    }

    [Fact]
    public async Task GetTestAuth_WithoutCookie_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/test-auth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTestAuth_WithInvalidCookie_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", "FarmKartAuth=invalid-token-value");

        // Act
        var response = await client.GetAsync("/api/auth/test-auth");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTestAuth_WithValidCookie_Returns200OK_WithAuthenticatedClaims()
    {
        // Arrange
        await SetupTestUserAsync("customer.cookie@test.com", "SecurePassword123!", "Customer Alice", Roles.Customer);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Log in to get the cookie
        var loginRequest = new LoginRequest("customer.cookie@test.com", "SecurePassword123!");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.True(loginResponse.Headers.Contains("Set-Cookie"));

        // Extract the raw cookie part
        var cookieHeader = loginResponse.Headers.GetValues("Set-Cookie").First();
        var cookieParts = cookieHeader.Split(';');
        var nameValuePair = cookieParts.First(p => p.Trim().StartsWith("FarmKartAuth=")).Trim();
        var tokenValue = nameValuePair.Split('=').Last();

        // Manual validation check to debug direct token issues
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "FarmKart",
            ValidAudience = "FarmKartUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ThisIsADevelopmentSecretKeyForTestingOnlyAndMustBeAtLeast32Bytes!"))
        };

        try
        {
            tokenHandler.ValidateToken(tokenValue, validationParameters, out _);
        }
        catch (Exception ex)
        {
            throw new Exception($"Manual JWT validation failed in test setup: {ex.Message}. Token: {tokenValue}", ex);
        }

        // Make subsequent request using a clean client and adding the Cookie header manually
        var authenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", nameValuePair);

        // Act
        var response = await authenticatedClient.GetAsync("/api/auth/test-auth");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var userClaims = await response.Content.ReadFromJsonAsync<TestAuthResult>();
        Assert.NotNull(userClaims);
        Assert.Equal("customer.cookie@test.com", userClaims.Email);
        Assert.Equal(Roles.Customer, userClaims.Role);
        Assert.False(string.IsNullOrWhiteSpace(userClaims.UserId));
    }

    [Fact]
    public async Task GetCurrentUser_WithValidCookie_Returns200OK_WithUserInfo()
    {
        // Arrange
        await SetupTestUserAsync("current.user@test.com", "SecurePassword123!", "Farmer Joe", Roles.Farmer);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var loginRequest = new LoginRequest("current.user@test.com", "SecurePassword123!");
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        Assert.True(loginResponse.Headers.Contains("Set-Cookie"));

        var cookieHeader = loginResponse.Headers.GetValues("Set-Cookie").First();
        var nameValuePair = cookieHeader.Split(';').First(p => p.Trim().StartsWith("FarmKartAuth=")).Trim();

        var authenticatedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", nameValuePair);

        // Act
        var response = await authenticatedClient.GetAsync("/api/auth/current-user");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userInfo = await response.Content.ReadFromJsonAsync<AuthUserResponse>();
        Assert.NotNull(userInfo);
        Assert.Equal("current.user@test.com", userInfo.Email);
        Assert.Equal(Roles.Farmer, userInfo.Role);
        Assert.Equal("Farmer Joe", userInfo.FullName);
        Assert.NotEqual(Guid.Empty, userInfo.UserId);
    }

    private record TestAuthResult(string UserId, string Email, string Role);
}
