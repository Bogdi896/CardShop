
using System.Net;
using System.Net.Http.Json;
using CardShop.Application.Common.Constants;
using CardShop.Application.DTOs.Auth;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Concurrency;

public class RegistrationAmbiguousCommitTests
{
    [Fact]
    public async Task Register_WhenCommitConfirmationIsLost_ShouldPreserveRegistration()
    {
        // =====================================================
        // ARRANGE
        // =====================================================

        using var factory = new CommitFailureWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var registrationId = Guid.NewGuid();
        var email = $"registration-{Guid.NewGuid():N}@example.com";

        var request = new RegisterRequest
        {
            RegistrationId = registrationId,
            Email = email,
            Password = "CardShop123!",
            ConfirmPassword = "CardShop123!"
        };

        // =====================================================
        // ACT
        // =====================================================

        // The next successful transaction commit will throw
        // our simulated lost-confirmation exception.
        factory.CommitInterceptor.Arm();

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        var responseBody = await response.Content.ReadAsStringAsync();

        // =====================================================
        // ASSERT: REGISTRATION RESPONSE
        // =====================================================

        // AuthService deliberately rejects a recovered
        // registration instead of issuing a JWT.
        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            $"registration returned: {responseBody}");

        factory.CommitInterceptor.FailureCount.Should().Be(
            1,
            "the commit confirmation failure must be injected");

        // =====================================================
        // ASSERT: DATABASE STATE
        // =====================================================

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var users = await context.Users
                .AsNoTracking()
                .Where(user =>
                    user.RegistrationId == registrationId)
                .ToListAsync();

            users.Should().ContainSingle();

            var user = users.Single();

            user.Email.Should().Be(email);

            var roles = await userManager.GetRolesAsync(user);

            roles.Should().Contain(RoleNames.Customer);
            roles.Should().NotContain(RoleNames.Admin);

            var carts = await context.Carts
                .AsNoTracking()
                .Include(cart => cart.Items)
                .Where(cart => cart.UserId == user.Id)
                .ToListAsync();

            carts.Should().ContainSingle();
            carts.Single().Items.Should().BeEmpty();
        }

        // =====================================================
        // ASSERT: RETRYING REGISTRATION
        // =====================================================

        using var retryResponse = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        retryResponse.StatusCode.Should().Be(
            HttpStatusCode.BadRequest);

        // =====================================================
        // ASSERT: NORMAL LOGIN STILL WORKS
        // =====================================================

        using var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = request.Password
            });

        loginResponse.StatusCode.Should().Be(
            HttpStatusCode.OK);

        var auth = await loginResponse.Content
            .ReadFromJsonAsync<AuthResponse>();

        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Roles.Should().Contain(RoleNames.Customer);

        // The interceptor should have thrown exactly once.
        factory.CommitInterceptor.FailureCount.Should().Be(1);
    }
}
