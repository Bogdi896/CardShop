
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

namespace CardShop.IntegrationTests.Controllers;

public class AuthControllerTests
{
    private static RegisterRequest ValidRegisterRequest() => new()
    {
        RegistrationId = Guid.NewGuid(),
        Email = "customer@example.com",
        Password = "CardShop123!",
        ConfirmPassword = "CardShop123!"
    };

    // =========================================================
    // SUCCESSFUL REGISTRATION
    // =========================================================

    [Fact]
    public async Task Register_WithValidRequest_ShouldCreateCustomer()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var request = ValidRegisterRequest();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Email.Should().Be(request.Email);
        auth.Roles.Should().Contain(RoleNames.Customer);
        auth.Roles.Should().NotContain(RoleNames.Admin);

        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var user = await userManager.FindByEmailAsync(
            request.Email);

        user.Should().NotBeNull();
        user!.RegistrationId.Should().Be(request.RegistrationId);
        user.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBe(request.Password);

        var roles = await userManager.GetRolesAsync(user);
        roles.Should().Contain(RoleNames.Customer);

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
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var firstRequest = ValidRegisterRequest();

        using var firstResponse = await client.PostAsJsonAsync(
            "/auth/register",
            firstRequest);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Same email, but a DIFFERENT registration attempt.
        var secondRequest = ValidRegisterRequest();
        secondRequest.RegistrationId = Guid.NewGuid();

        using var secondResponse = await client.PostAsJsonAsync(
            "/auth/register",
            secondRequest);

        secondResponse.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    // =========================================================
    // DUPLICATE REGISTRATION ID
    // =========================================================

    [Fact]
    public async Task Register_WithSameRegistrationId_ShouldRejectDuplicate()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var request = ValidRegisterRequest();

        using var firstResponse = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var secondResponse = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        secondResponse.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var users = await context.Users
            .AsNoTracking()
            .Where(user =>
                user.RegistrationId == request.RegistrationId)
            .ToListAsync();

        users.Should().ContainSingle();

        var carts = await context.Carts
            .AsNoTracking()
            .Where(cart => cart.UserId == users.Single().Id)
            .ToListAsync();

        carts.Should().ContainSingle();
    }

    // =========================================================
    // EMPTY REGISTRATION ID
    // =========================================================

    [Fact]
    public async Task Register_WithEmptyRegistrationId_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var request = ValidRegisterRequest();
        request.RegistrationId = Guid.Empty;

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================
    // MISMATCHED PASSWORDS
    // =========================================================

    [Fact]
    public async Task Register_WithMismatchedPasswords_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var request = ValidRegisterRequest();
        request.ConfirmPassword = "Different123!";

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================
    // SUCCESSFUL LOGIN
    // =========================================================

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwt()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var registerRequest = ValidRegisterRequest();

        using var registerResponse = await client.PostAsJsonAsync(
            "/auth/register",
            registerRequest);

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Roles.Should().Contain(RoleNames.Customer);
        auth.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    // =========================================================
    // INCORRECT PASSWORD
    // =========================================================

    [Fact]
    public async Task Login_WithIncorrectPassword_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        using var registerResponse = await client.PostAsJsonAsync(
            "/auth/register",
            ValidRegisterRequest());

        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = "customer@example.com",
                Password = "WrongPassword123!"
            });

        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    // =========================================================
    // NONEXISTENT USER
    // =========================================================

    [Fact]
    public async Task Login_WithNonexistentUser_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = "unknown@example.com",
                Password = "SomePassword123!"
            });

        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }
}
