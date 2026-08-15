using FarmKart.API.Extensions;
using FarmKart.Application.DependencyInjection;
using FarmKart.Infrastructure.DependencyInjection;
using FarmKart.Infrastructure.Persistence;
using FarmKart.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPresentation()
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FarmKartDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
    }
    catch
    {
        // Fallback for custom environment configurations
    }
}

await DatabaseSchemaMigrationSeeder.EnsureSchemaUpdatedAsync(app.Services);
await IdentityRoleSeeder.SeedRolesAsync(app.Services);
await AssignmentBackfillSeeder.SyncAcceptedAssignmentsAsync(app.Services);

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});
app.UseCors("CorsPolicy");
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

public partial class Program { }
