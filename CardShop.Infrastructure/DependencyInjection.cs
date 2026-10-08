using CardShop.Application.Interfaces.Repositories;
using CardShop.Infrastructure.Persistence;
using CardShop.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString =
                configuration.GetConnectionString("CardShopDatabase")
                ?? throw new InvalidOperationException(
                    "Connection string 'CardShopDatabase' was not found.");

            services.AddDbContext<CardShopDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddScoped<IProductRepository, ProductRepository>();

            return services;
        }
    }
}