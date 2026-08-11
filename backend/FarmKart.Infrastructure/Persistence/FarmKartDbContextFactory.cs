using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;
using System.Text.Json;

namespace FarmKart.Infrastructure.Persistence;

public sealed class FarmKartDbContextFactory : IDesignTimeDbContextFactory<FarmKartDbContext>
{
    public FarmKartDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var apiPath = Path.Combine(basePath, "FarmKart.API");
        if (!Directory.Exists(apiPath))
        {
            var parentPath = Path.Combine(basePath, "../FarmKart.API");
            if (Directory.Exists(parentPath))
            {
                basePath = Path.GetFullPath(parentPath);
            }
        }
        else
        {
            basePath = apiPath;
        }

        var appsettingsPath = Path.Combine(basePath, "appsettings.json");
        var connectionString = GetConnectionStringFromJson(appsettingsPath);

        // Fallback to Development settings if available or if the connection string wasn't found
        var devSettingsPath = Path.Combine(basePath, "appsettings.Development.json");
        if (File.Exists(devSettingsPath))
        {
            var devConnStr = GetConnectionStringFromJson(devSettingsPath);
            if (!string.IsNullOrEmpty(devConnStr))
            {
                connectionString = devConnStr;
            }
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        var optionsBuilder = new DbContextOptionsBuilder<FarmKartDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new FarmKartDbContext(optionsBuilder.Options);
    }

    private string? GetConnectionStringFromJson(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var connStrings) &&
                connStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
            {
                return defaultConnection.GetString();
            }
        }
        catch
        {
            // Ignore parse errors, return null to fallback
        }

        return null;
    }
}
