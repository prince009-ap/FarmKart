using FarmKart.Application.Abstractions.Persistence;
using FarmKart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FarmKart.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("FarmKartDatabase")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        services.AddDbContext<FarmKartDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IFarmKartDbContext>(provider => provider.GetRequiredService<FarmKartDbContext>());

        return services;
    }
}
