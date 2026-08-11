using FarmKart.Application.DependencyInjection;
using FarmKart.Infrastructure.DependencyInjection;

namespace FarmKart.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }
}
