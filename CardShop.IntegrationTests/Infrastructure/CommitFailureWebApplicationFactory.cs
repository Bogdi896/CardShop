
using CardShop.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.IntegrationTests.Infrastructure;

public class CommitFailureWebApplicationFactory
    : SqlServerWebApplicationFactory
{
    public CommitConfirmationFailureInterceptor
        CommitInterceptor
    { get; } = new();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<CardShopDbContext>(
                options =>
                {
                    options.AddInterceptors(CommitInterceptor);
                });
        });
    }
}
