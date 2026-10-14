using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Infrastructure.Authentication;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using CardShop.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
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

            services.AddDataProtection();

            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;

                options.User.RequireUniqueEmail = true;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<CardShopDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();
            services.Configure<JwtSettings>(
    configuration.GetSection(JwtSettings.SectionName));

            services.AddScoped<ITokenService, TokenService>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ICartRepository, CartRepository>();

            return services;
        }
    }
}