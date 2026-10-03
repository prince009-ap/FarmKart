using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FarmKart.Tests.Infrastructure;

internal static class TestSqlServer
{
    private const string FallbackConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=FarmKartDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    private static readonly Lazy<string> BaseConnectionString = new(ResolveBaseConnectionString);

    public static string ConnectionString(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(BaseConnectionString.Value)
        {
            InitialCatalog = databaseName,
            TrustServerCertificate = true
        };

        builder["Encrypt"] = "False";

        if (!builder.ContainsKey("MultipleActiveResultSets"))
        {
            builder.MultipleActiveResultSets = true;
        }

        return builder.ConnectionString;
    }

    private static string ResolveBaseConnectionString()
    {
        var environmentOverride = Environment.GetEnvironmentVariable("FARMKART_TEST_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return environmentOverride;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection") ?? FallbackConnectionString;
    }
}

