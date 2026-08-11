using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FarmKart.Infrastructure.Persistence;

public sealed class FarmKartDbContextFactory : IDesignTimeDbContextFactory<FarmKartDbContext>
{
    public FarmKartDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FarmKartDbContext>();
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString);

        return new FarmKartDbContext(optionsBuilder.Options);
    }
}
