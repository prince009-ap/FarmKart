using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Identity;
using FarmKart.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq;
using Xunit;

namespace FarmKart.Tests.Infrastructure;

public class IdentityFoundationTests
{
    [Fact]
    public void ApplicationUser_Uses_Guid_As_PrimaryKey()
    {
        var user = new ApplicationUser();
        Assert.IsAssignableFrom<IdentityUser<Guid>>(user);
        Assert.Equal(typeof(Guid), typeof(ApplicationUser).GetProperty("Id")!.PropertyType);
    }

    [Fact]
    public void DbContext_Inherits_From_IdentityDbContext()
    {
        using var context = CreateContext();
        Assert.IsAssignableFrom<IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>>(context);
    }

    [Fact]
    public void DbContext_Contains_Identity_And_Domain_Entities()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();

        // Identity entities
        Assert.Contains(typeof(ApplicationUser), entityTypes);
        Assert.Contains(typeof(IdentityRole<Guid>), entityTypes);
        Assert.Contains(typeof(IdentityUserClaim<Guid>), entityTypes);
        Assert.Contains(typeof(IdentityUserRole<Guid>), entityTypes);
        Assert.Contains(typeof(IdentityUserLogin<Guid>), entityTypes);
        Assert.Contains(typeof(IdentityRoleClaim<Guid>), entityTypes);
        Assert.Contains(typeof(IdentityUserToken<Guid>), entityTypes);

        // Domain entities
        Assert.Contains(typeof(FarmerProfile), entityTypes);
        Assert.Contains(typeof(WorkerProfile), entityTypes);
        Assert.Contains(typeof(CustomerProfile), entityTypes);
    }

    [Fact]
    public void Profile_UserId_Has_Unique_Index_And_Restrict_Delete_Behavior()
    {
        using var context = CreateContext();
        var designTimeModel = context.GetService<IDesignTimeModel>().Model;

        VerifyProfileMapping<FarmerProfile>(designTimeModel);
        VerifyProfileMapping<WorkerProfile>(designTimeModel);
        VerifyProfileMapping<CustomerProfile>(designTimeModel);
    }

    private void VerifyProfileMapping<TProfile>(IModel model)
        where TProfile : class
    {
        var entityType = model.FindEntityType(typeof(TProfile));
        Assert.NotNull(entityType);

        var userIdProp = entityType.FindProperty("UserId");
        Assert.NotNull(userIdProp);
        Assert.Equal(typeof(Guid), userIdProp.ClrType);

        // Check index is unique
        var index = entityType.GetIndexes().SingleOrDefault(idx => idx.Properties.Any(p => p.Name == "UserId"));
        Assert.NotNull(index);
        Assert.True(index.IsUnique);

        // Check foreign key behavior
        var fk = entityType.GetForeignKeys()
            .SingleOrDefault(f => f.Properties.Any(p => p.Name == "UserId") && f.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    private static FarmKartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb.Tests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new FarmKartDbContext(options);
    }
}
