
using CardShop.Application.Common.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CardShop.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        // Ensure both roles exist.
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
                        $"Could not create role '{roleName}': " +
                        string.Join(", ",
                            result.Errors.Select(e => e.Description)));
                }
            }
        }

        // Never seed an administrator from this code
        // outside the Development environment.
        if (!environment.IsDevelopment())
        {
            return;
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        // Admin seeding is optional.
        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(
                admin,
                adminPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create development administrator: " +
                    string.Join(", ",
                        createResult.Errors.Select(e => e.Description)));
            }
        }
        else if (!await userManager.IsInRoleAsync(
            admin,
            RoleNames.Admin))
        {
            // Do not promote an existing account automatically.
            throw new InvalidOperationException(
                "The configured administrator email already belongs " +
                "to an account without the Admin role.");
        }

        if (!await userManager.IsInRoleAsync(
            admin,
            RoleNames.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(
                admin,
                RoleNames.Admin);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not assign the Admin role.");
            }
        }
    }
}
