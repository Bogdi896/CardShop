
using CardShop.Application.Common.Constants;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;

namespace CardShop.IntegrationTests.Infrastructure;

public class CardShopWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    private readonly string _jwtSecret =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Make configuration available during Program.cs startup.
        builder.UseSetting(
            "ConnectionStrings:CardShopDb",
            "Server=localhost;Database=UnusedTestDb;Trusted_Connection=True;");

        builder.UseSetting("Jwt:Issuer", "CardShop.IntegrationTests");
        builder.UseSetting("Jwt:Audience", "CardShop.TestClient");
        builder.UseSetting("Jwt:SecretKey", _jwtSecret);
        builder.UseSetting("Jwt:ExpirationMinutes", "60");

        builder.ConfigureServices(services =>
        {
            // Remove SQL Server provider configuration.
            services.RemoveAll<DbContextOptions<CardShopDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<CardShopDbContext>>();

            // Create an isolated SQLite database.
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<CardShopDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        _ = Services;

        using var scope = Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        await context.Database.EnsureCreatedAsync();

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
                        $"Failed to create test role: {roleName}");
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}
