
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CardShop.Application.Common.Constants;
using CardShop.Application.DTOs.Auth;
using CardShop.Infrastructure.Identity;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Controllers;

public class AuthorizationTests
{
    [Fact]
    public async Task AdminEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/admin/products");

        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithCustomerToken_ShouldReturnForbidden()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                Email = "customer@example.com",
                Password = "CardShop123!",
                ConfirmPassword = "CardShop123!"
            });

        registration.EnsureSuccessStatusCode();

        var auth = await registration.Content
            .ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        var response = await client.GetAsync("/admin/products");

        response.StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoint_WithAdminToken_ShouldReturnOk()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        // Create an admin user directly through Identity.
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var admin = new ApplicationUser
            {
                UserName = "admin@example.com",
                Email = "admin@example.com",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(
                admin,
                "AdminPassword123!");

            createResult.Succeeded.Should().BeTrue();

            var roleResult = await userManager.AddToRoleAsync(
                admin,
                RoleNames.Admin);

            roleResult.Succeeded.Should().BeTrue();
        }

        // Log in using the real authentication endpoint.
        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = "admin@example.com",
                Password = "AdminPassword123!"
            });

        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content
            .ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        var response = await client.GetAsync("/admin/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PublicProducts_WithoutToken_ShouldReturnOk()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
