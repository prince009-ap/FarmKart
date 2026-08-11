using FarmKart.Domain.Entities;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FarmKart.Tests.Infrastructure;

public class FarmKartDbContextTests
{
    [Fact]
    public void DbContext_Can_Build_Model_With_Core_Entities()
    {
        using var context = CreateContext();

        var entityTypes = context.Model.GetEntityTypes().Select(entity => entity.ClrType).ToHashSet();

        Assert.Contains(typeof(FarmerProfile), entityTypes);
        Assert.Contains(typeof(Job), entityTypes);
        Assert.Contains(typeof(MachineryRental), entityTypes);
        Assert.Contains(typeof(CropListing), entityTypes);
        Assert.Contains(typeof(Auction), entityTypes);
        Assert.Contains(typeof(Order), entityTypes);
        Assert.Contains(typeof(Conversation), entityTypes);
        Assert.Contains(typeof(Review), entityTypes);
    }

    [Fact]
    public void Critical_Historical_Relationships_Do_Not_Use_Cascade_Delete()
    {
        using var context = CreateContext();

        Assert.Equal(DeleteBehavior.Restrict, FindForeignKey<WorkerPayment>(nameof(WorkerPayment.FarmerProfile))!.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, FindForeignKey<WorkerPayment>(nameof(WorkerPayment.WorkerAssignment))!.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, FindForeignKey<MachineryRental>(nameof(MachineryRental.OwnerFarmerProfile))!.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, FindForeignKey<OrderItem>(nameof(OrderItem.Order))!.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, FindForeignKey<Bid>(nameof(Bid.Auction))!.DeleteBehavior);

        IForeignKey? FindForeignKey<TEntity>(string navigationName)
            where TEntity : class
        {
            return context.Model
                .FindEntityType(typeof(TEntity))?
                .GetForeignKeys()
                .SingleOrDefault(foreignKey => foreignKey.DependentToPrincipal?.Name == navigationName);
        }
    }

    [Fact]
    public void Model_Applies_Key_Indexes_And_Check_Constraints()
    {
        using var context = CreateContext();

        var designTimeModel = context.GetService<IDesignTimeModel>().Model;
        var farmerEntity = designTimeModel.FindEntityType(typeof(FarmerProfile));
        var reviewEntity = designTimeModel.FindEntityType(typeof(Review));
        var auctionEntity = designTimeModel.FindEntityType(typeof(Auction));

        Assert.NotNull(farmerEntity);
        Assert.NotNull(reviewEntity);
        Assert.NotNull(auctionEntity);

        Assert.Contains(
            farmerEntity!.GetIndexes(),
            index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(FarmerProfile.UserId)));

        Assert.Contains(
            reviewEntity!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Review_Rating_Range");

        Assert.Contains(
            auctionEntity!.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Auction_EndTime_After_StartTime");
    }

    private static FarmKartDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FarmKartDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb.Tests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new FarmKartDbContext(options);
    }
}
