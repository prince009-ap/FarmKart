using FarmKart.Domain.Common;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class IdentityRoleSeedingTests
{
    [Fact]
    public void RoleConstants_Exist_With_CorrectValues()
    {
        Assert.Equal("Farmer", Roles.Farmer);
        Assert.Equal("Worker", Roles.Worker);
        Assert.Equal("Customer", Roles.Customer);
    }

    [Fact]
    public async Task RoleSeeder_SeedsRoles_Successfully_And_IsIdempotent()
    {
        var dbName = $"FarmKartDb_SeedingTest_{Guid.NewGuid()}";

        // Setup service collection
        var services = new ServiceCollection();

        services.AddDbContext<FarmKartDbContext>(options =>
            options.UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True"));

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FarmKartDbContext>();

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();

        // Ensure database is clean and created
        try
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();
            }

            // Act - Run first time (Seeding)
            await IdentityRoleSeeder.SeedRolesAsync(serviceProvider);

            // Assert - Roles created
            using (var scope = serviceProvider.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

                Assert.True(await roleManager.RoleExistsAsync(Roles.Farmer));
                Assert.True(await roleManager.RoleExistsAsync(Roles.Worker));
                Assert.True(await roleManager.RoleExistsAsync(Roles.Customer));

                var totalRoles = await roleManager.Roles.CountAsync();
                Assert.Equal(3, totalRoles);
            }

            // Act - Run second time (Idempotency check)
            await IdentityRoleSeeder.SeedRolesAsync(serviceProvider);

            // Assert - No duplicate roles created
            using (var scope = serviceProvider.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

                Assert.True(await roleManager.RoleExistsAsync(Roles.Farmer));
                Assert.True(await roleManager.RoleExistsAsync(Roles.Worker));
                Assert.True(await roleManager.RoleExistsAsync(Roles.Customer));

                var totalRoles = await roleManager.Roles.CountAsync();
                Assert.Equal(3, totalRoles); // Still exactly 3
            }
        }
        finally
        {
            // Cleanup database
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
                await context.Database.EnsureDeletedAsync();
            }
        }
    }
}
