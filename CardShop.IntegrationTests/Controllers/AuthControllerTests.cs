
using System.Net;
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

public class AuthControllerTests
{
    private static RegisterRequest ValidRegisterRequest() => new()
    {
        Email = "customer@example.com",
        Password = "CardShop123!",
        ConfirmPassword = "CardShop123!"
    };

    [Fact]
    public async Task Register_WithValidRequest_ShouldCreateCustomer()
    {
        // Arrange
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(
            "/auth/register",
            ValidRegisterRequest());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await response.Content
            .ReadFromJsonAsync<AuthResponse>();

        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Email.Should().Be("customer@example.com");
        auth.Roles.Should().Contain(RoleNames.Customer);
        auth.Roles.Should().NotContain(RoleNames.Admin);

        // Verify the user exists in the database.
        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(
            "customer@example.com");

        user.Should().NotBeNull();
        user!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotBe("CardShop123!");

        var roles = await userManager.GetRolesAsync(user);
        roles.Should().Contain(RoleNames.Customer);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var request = ValidRegisterRequest();

        var firstResponse = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        secondResponse.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var request = ValidRegisterRequest();
        request.ConfirmPassword = "Different123!";

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnJwt()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var registerRequest = ValidRegisterRequest();

        await client.PostAsJsonAsync(
            "/auth/register",
            registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = registerRequest.Email,
            Password = registerRequest.Password
        };

        var response = await client.PostAsJsonAsync(
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

    [Fact]
    public async Task Login_WithIncorrectPassword_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/auth/register",
            ValidRegisterRequest());

        var response = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = "customer@example.com",
                Password = "WrongPassword123!"
            });

        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
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
