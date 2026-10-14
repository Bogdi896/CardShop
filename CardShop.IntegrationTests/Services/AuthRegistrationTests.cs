
using CardShop.Application.Common.Constants;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Interfaces.Services;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Services;

public class AuthRegistrationTests
{
    private static RegisterRequest ValidRequest(
        string email = "customer@example.com") => new()
        {
            Email = email,
            Password = "CardShop123!",
            ConfirmPassword = "CardShop123!"
        };

    // =========================================================
    // SUCCESSFUL REGISTRATION
    // =========================================================

    [Fact]
    public async Task Register_WithValidRequest_ShouldPersistUserAndCustomerRole()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var scope = factory.Services.CreateScope();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var response = await authService.RegisterAsync(ValidRequest());

        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.Roles.Should().Contain(RoleNames.Customer);
        response.Roles.Should().NotContain(RoleNames.Admin);

        using var verificationScope = factory.Services.CreateScope();

        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(
            "customer@example.com");

        user.Should().NotBeNull();
        user!.Id.Should().Be(response.UserId);

        var roles = await userManager.GetRolesAsync(user);

        roles.Should().Contain(RoleNames.Customer);
        roles.Should().NotContain(RoleNames.Admin);
    }

    // =========================================================
    // DUPLICATE EMAIL
    // =========================================================

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldNotCreateAnotherUser()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var scope = factory.Services.CreateScope();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        await authService.RegisterAsync(ValidRequest());

        Func<Task> act = () =>
            authService.RegisterAsync(ValidRequest());

        await act.Should().ThrowAsync<RegistrationException>();

        using var verificationScope = factory.Services.CreateScope();

        var context = verificationScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var count = await context.Users.CountAsync(
            user => user.Email == "customer@example.com");

        count.Should().Be(1);
    }

    // =========================================================
    // INVALID PASSWORD
    // =========================================================

    [Fact]
    public async Task Register_WithInvalidPassword_ShouldNotCreateUser()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var scope = factory.Services.CreateScope();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var request = ValidRequest();
        request.Password = "weakpass";
        request.ConfirmPassword = "weakpass";

        Func<Task> act = () =>
            authService.RegisterAsync(request);

        await act.Should().ThrowAsync<RegistrationException>();

        using var verificationScope = factory.Services.CreateScope();

        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(request.Email);

        user.Should().BeNull();
    }

    // =========================================================
    // TRANSACTION ROLLBACK
    // =========================================================

    [Fact]
    public async Task Register_WhenCustomerRoleMissing_ShouldRollbackUserCreation()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        // Arrange: Remove the Customer role to force registration failure.
        using (var setupScope = factory.Services.CreateScope())
        {
            var roleManager = setupScope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var customerRole = await roleManager.FindByNameAsync(
                RoleNames.Customer);

            customerRole.Should().NotBeNull();

            var deleteResult = await roleManager.DeleteAsync(customerRole!);

            deleteResult.Succeeded.Should().BeTrue();
        }

        // Act: Attempt registration without the required Customer role.
        using (var registrationScope = factory.Services.CreateScope())
        {
            var authService = registrationScope.ServiceProvider
                .GetRequiredService<IAuthService>();

            Func<Task> act = () =>
                authService.RegisterAsync(ValidRequest());

            await act.Should().ThrowAsync<Exception>()
                .Where(exception =>
                    exception is RegistrationException ||
                    exception is InvalidOperationException);
        }

        // Assert: The user must not exist because the transaction rolled back.
        using var verificationScope = factory.Services.CreateScope();

        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(
            "customer@example.com");

        user.Should().BeNull();
    }
}
