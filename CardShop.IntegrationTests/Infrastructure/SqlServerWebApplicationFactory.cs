
using System.Security.Cryptography;
using CardShop.Application.Common.Constants;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.IntegrationTests.Infrastructure;

public class SqlServerWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private const string TestDatabaseName = "CardShopConcurrencyTests";

    private readonly string _connectionString =
    GetTestConnectionString();

    private static string GetTestConnectionString()
    {
        var configuredConnectionString =
            Environment.GetEnvironmentVariable(
                "CARDSHOP_TEST_SQLSERVER");

        var connectionString =
            !string.IsNullOrWhiteSpace(configuredConnectionString)
                ? configuredConnectionString
                : $@"Server=(localdb)\MSSQLLocalDB;
                 Database={TestDatabaseName};
                 Trusted_Connection=True;
                 TrustServerCertificate=True";

        var builder = new SqlConnectionStringBuilder(connectionString);

        if (!string.Equals(
            builder.InitialCatalog,
            TestDatabaseName,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration tests must use '{TestDatabaseName}'.");
        }

        return builder.ConnectionString;
    }

    private readonly string _jwtSecret =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:CardShopDatabase",
            _connectionString);

        builder.UseSetting(
            "Jwt:Issuer",
            "CardShop.ConcurrencyTests");

        builder.UseSetting(
            "Jwt:Audience",
            "CardShop.TestClient");

        builder.UseSetting(
            "Jwt:SecretKey",
            _jwtSecret);

        builder.UseSetting(
            "Jwt:ExpirationMinutes",
            "60");
    }

    public async Task InitializeDatabaseAsync()
    {
        // Verify the connection string before doing anything.
        var connectionBuilder =
            new SqlConnectionStringBuilder(_connectionString);

        if (!string.Equals(
            connectionBuilder.InitialCatalog,
            TestDatabaseName,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Concurrency tests must use the dedicated test database.");
        }

        // Initialize the test host.
        _ = Services;

        using var scope = Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        // Apply all existing EF Core migrations.
        await context.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in new[]
        {
            RoleNames.Admin,
            RoleNames.Customer
        })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{roleName}'.");
                }
            }
        }
    }
}
