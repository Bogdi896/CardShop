
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Concurrency;

public class SqlServerSetupTests
{
    [Fact]
    public async Task TestFactory_ShouldConnectToDedicatedDatabase()
    {
        using var factory = new SqlServerWebApplicationFactory();

        await factory.InitializeDatabaseAsync();

        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        context.Database.IsSqlServer().Should().BeTrue();

        context.Database.GetDbConnection().Database
            .Should().Be("CardShopConcurrencyTests");

        (await context.Database.CanConnectAsync())
            .Should().BeTrue();
    }
}
