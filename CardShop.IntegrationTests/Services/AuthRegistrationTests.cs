
using CardShop.Application.Common.Constants;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Interfaces.Services;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using FluentValidation;
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
            RegistrationId = Guid.NewGuid(),
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

        var request = ValidRequest();

        var response = await authService.RegisterAsync(request);

        response.AccessToken.Should().NotBeNullOrWhiteSpace();
        response.Roles.Should().Contain(RoleNames.Customer);
        response.Roles.Should().NotContain(RoleNames.Admin);

        using var verificationScope = factory.Services.CreateScope();

        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var context = verificationScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var user = await userManager.FindByEmailAsync(
            request.Email);

        user.Should().NotBeNull();
        user!.Id.Should().Be(response.UserId);
        user.RegistrationId.Should().Be(request.RegistrationId);

        var roles = await userManager.GetRolesAsync(user);

        roles.Should().Contain(RoleNames.Customer);
        roles.Should().NotContain(RoleNames.Admin);

        // Verify that registration also created the user's cart.
        var carts = await context.Carts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .Where(cart => cart.UserId == user.Id)
            .ToListAsync();

        carts.Should().ContainSingle();
        carts.Single().Items.Should().BeEmpty();
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

        var firstRequest = ValidRequest();

        await authService.RegisterAsync(firstRequest);

        // Same email, but a different registration attempt.
        var secondRequest = ValidRequest();

        secondRequest.RegistrationId = Guid.NewGuid();

        Func<Task> act = () =>
            authService.RegisterAsync(secondRequest);

        await act.Should().ThrowAsync<RegistrationException>();

        using var verificationScope = factory.Services.CreateScope();

        var context = verificationScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var users = await context.Users
            .AsNoTracking()
            .Where(user => user.Email == firstRequest.Email)
            .ToListAsync();

        users.Should().ContainSingle();

        users.Single().RegistrationId.Should()
            .Be(firstRequest.RegistrationId);

        // Duplicate registration must not create another cart.
        var carts = await context.Carts
            .AsNoTracking()
            .Where(cart => cart.UserId == users.Single().Id)
            .ToListAsync();

        carts.Should().ContainSingle();
    }

    // =========================================================
    // DUPLICATE REGISTRATION ID
    // =========================================================

    [Fact]
    public async Task Register_WithDuplicateRegistrationId_ShouldRejectRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var scope = factory.Services.CreateScope();

        var authService = scope.ServiceProvider
            .GetRequiredService<IAuthService>();

        var request = ValidRequest();

        // First registration succeeds.
        await authService.RegisterAsync(request);

        // Repeating the same RegistrationId must be rejected.
        Func<Task> act = () =>
            authService.RegisterAsync(request);

        await act.Should().ThrowAsync<RegistrationException>();

        using var verificationScope = factory.Services.CreateScope();

        var context = verificationScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        // Exactly one user should exist for this RegistrationId.
        var users = await context.Users
            .AsNoTracking()
            .Where(user =>
                user.RegistrationId == request.RegistrationId)
            .ToListAsync();

        users.Should().ContainSingle();

        // Exactly one cart should exist for that user.
        var carts = await context.Carts
            .AsNoTracking()
            .Where(cart => cart.UserId == users.Single().Id)
            .ToListAsync();

        carts.Should().ContainSingle();
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

        // FluentValidation rejects the password before Identity
        // attempts to create the user.
        await act.Should().ThrowAsync<ValidationException>();

        using var verificationScope = factory.Services.CreateScope();

        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var context = verificationScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var user = await userManager.FindByEmailAsync(
            request.Email);

        user.Should().BeNull();

        var registrationExists = await context.Users
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.RegistrationId == request.RegistrationId);

        registrationExists.Should().BeFalse();

        // No cart should have been created for the failed registration.
        var cartCount = await context.Carts.CountAsync();

        cartCount.Should().Be(0);
    }

    // =========================================================
    // TRANSACTION ROLLBACK
    // =========================================================

    [Fact]
    public async Task Register_WhenCustomerRoleMissing_ShouldRollbackUserCreation()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var request = ValidRequest();

        // Remove the Customer role to force registration failure.
        using (var setupScope = factory.Services.CreateScope())
        {
            var roleManager = setupScope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            var customerRole = await roleManager.FindByNameAsync(
                RoleNames.Customer);

            customerRole.Should().NotBeNull();

            var deleteResult = await roleManager.DeleteAsync(
                customerRole!);

            deleteResult.Succeeded.Should().BeTrue();
        }

        // Attempt registration without the required Customer role.
        using (var registrationScope = factory.Services.CreateScope())
        {
            var authService = registrationScope.ServiceProvider
                .GetRequiredService<IAuthService>();

            Func<Task> act = () =>
                authService.RegisterAsync(request);

            await act.Should().ThrowAsync<Exception>()
                .Where(exception =>
                    exception is RegistrationException ||
                    exception is InvalidOperationException);
        }

        // Verify that the transaction rolled back everything.
        using var verificationScope = factory.Services.CreateScope();

        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var context = verificationScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var user = await userManager.FindByEmailAsync(
            request.Email);

        user.Should().BeNull();

        var registrationExists = await context.Users
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.RegistrationId == request.RegistrationId);

        registrationExists.Should().BeFalse();

        var cartCount = await context.Carts.CountAsync();

        cartCount.Should().Be(0);
    }
}
