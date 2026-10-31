
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CardShop.Application.Common.Constants;
using CardShop.Application.DTOs.Auth;
using CardShop.Infrastructure.Identity;
using CardShop.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.IntegrationTests.Helpers;

public static class AuthenticationHelper
{
    public static async Task AuthenticateAsAdminAsync(
        HttpClient client,
        CardShopWebApplicationFactory factory)
    {
        const string email = "admin@example.com";
        const string password = "AdminPassword123!";

        // Create an administrator directly in the test database.
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var admin = await userManager.FindByEmailAsync(email);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(
                    admin,
                    password);

                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join(", ",
                            createResult.Errors.Select(e => e.Description)));
                }

                var roleResult = await userManager.AddToRoleAsync(
                    admin,
                    RoleNames.Admin);

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join(", ",
                            roleResult.Errors.Select(e => e.Description)));
                }
            }
            else if (!await userManager.IsInRoleAsync(
                admin,
                RoleNames.Admin))
            {
                throw new InvalidOperationException(
                    "The test administrator exists without the Admin role.");
            }
        }

        // Log in through the real API.
        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = password
            });

        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content
            .ReadFromJsonAsync<AuthResponse>();

        if (auth is null ||
            string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            throw new InvalidOperationException(
                "Administrator login did not return a JWT.");
        }

        // Attach the JWT to subsequent HTTP requests.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth.AccessToken);
    }
}
