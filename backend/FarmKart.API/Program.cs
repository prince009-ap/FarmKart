using FarmKart.API.Extensions;
using FarmKart.Application.DependencyInjection;
using FarmKart.Infrastructure.DependencyInjection;
using FarmKart.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPresentation()
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

await IdentityRoleSeeder.SeedRolesAsync(app.Services);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "FarmKart.API",
    utcTime = DateTime.UtcNow
}));

app.Run();
