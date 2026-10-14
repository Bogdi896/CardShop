
using CardShop.Application.Common.Constants;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Validators.Auth;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CardShop.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;

    private readonly AuthService _service;

    public AuthServiceTests()
    {
        // Mock the Identity user store.
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();

        // UserManager requires an IUserStore.
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        // SignInManager requires UserManager and HTTP-related services.
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());

        _tokenServiceMock = new Mock<ITokenService>();

        // LoginAsync does not access DbContext.
        // We still provide one because the constructor requires it.
        var dbOptions = new DbContextOptionsBuilder<CardShopDbContext>()
            .Options;

        var context = new CardShopDbContext(dbOptions);

        _service = new AuthService(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenServiceMock.Object,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            context);
    }

    private static LoginRequest ValidLoginRequest() => new()
    {
        Email = "customer@example.com",
        Password = "CardShop123!"
    };

    private static ApplicationUser ExistingUser() => new()
    {
        Id = "user-123",
        UserName = "customer@example.com",
        Email = "customer@example.com"
    };

    // =========================================================
    // SUCCESSFUL LOGIN
    // =========================================================

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnAuthResponse()
    {
        // Arrange
        var request = ValidLoginRequest();
        var user = ExistingUser();

        var roles = new List<string> { RoleNames.Customer };

        var expectedResponse = new AuthResponse
        {
            AccessToken = "test-jwt-token",
            UserId = user.Id,
            Email = user.Email!,
            Roles = roles,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };

        _userManagerMock
            .Setup(manager => manager.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _signInManagerMock
            .Setup(manager => manager.CheckPasswordSignInAsync(
                user,
                request.Password,
                true))
            .ReturnsAsync(SignInResult.Success);

        _userManagerMock
            .Setup(manager => manager.GetRolesAsync(user))
            .ReturnsAsync(roles);

        _tokenServiceMock
            .Setup(service => service.GenerateToken(
                user.Id,
                user.Email!,
                roles))
            .Returns(expectedResponse);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("test-jwt-token");
        result.UserId.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.Roles.Should().Contain(RoleNames.Customer);

        _tokenServiceMock.Verify(
            service => service.GenerateToken(
                user.Id,
                user.Email!,
                roles),
            Times.Once());
    }

    // =========================================================
    // USER DOES NOT EXIST
    // =========================================================

    [Fact]
    public async Task Login_WithNonexistentUser_ShouldReturnNull()
    {
        // Arrange
        var request = ValidLoginRequest();

        _userManagerMock
            .Setup(manager => manager.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().BeNull();

        _signInManagerMock.Verify(
            manager => manager.CheckPasswordSignInAsync(
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>(),
                It.IsAny<bool>()),
            Times.Never());

        _tokenServiceMock.Verify(
            service => service.GenerateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IList<string>>()),
            Times.Never());
    }

    // =========================================================
    // INCORRECT PASSWORD
    // =========================================================

    [Fact]
    public async Task Login_WithIncorrectPassword_ShouldReturnNull()
    {
        // Arrange
        var request = ValidLoginRequest();
        var user = ExistingUser();

        _userManagerMock
            .Setup(manager => manager.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _signInManagerMock
            .Setup(manager => manager.CheckPasswordSignInAsync(
                user,
                request.Password,
                true))
            .ReturnsAsync(SignInResult.Failed);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().BeNull();

        _tokenServiceMock.Verify(
            service => service.GenerateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IList<string>>()),
            Times.Never());
    }

    // =========================================================
    // LOCKED ACCOUNT
    // =========================================================

    [Fact]
    public async Task Login_WithLockedAccount_ShouldReturnNull()
    {
        // Arrange
        var request = ValidLoginRequest();
        var user = ExistingUser();

        _userManagerMock
            .Setup(manager => manager.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _signInManagerMock
            .Setup(manager => manager.CheckPasswordSignInAsync(
                user,
                request.Password,
                true))
            .ReturnsAsync(SignInResult.LockedOut);

        // Act
        var result = await _service.LoginAsync(request);

        // Assert
        result.Should().BeNull();

        _tokenServiceMock.Verify(
            service => service.GenerateToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IList<string>>()),
            Times.Never());
    }

    // =========================================================
    // INVALID EMAIL
    // =========================================================

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("missing-at-sign.com")]
    public async Task Login_WithInvalidEmail_ShouldThrowValidationException(
        string email)
    {
        // Arrange
        var request = ValidLoginRequest();
        request.Email = email;

        // Act
        Func<Task> act = async () =>
            await _service.LoginAsync(request);

        // Assert
        await act.Should()
            .ThrowAsync<ValidationException>();

        _userManagerMock.Verify(
            manager => manager.FindByEmailAsync(
                It.IsAny<string>()),
            Times.Never());
    }

    // =========================================================
    // EMPTY PASSWORD
    // =========================================================

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Login_WithEmptyPassword_ShouldThrowValidationException(
        string? password)
    {
        // Arrange
        var request = ValidLoginRequest();
        request.Password = password!;

        // Act
        Func<Task> act = async () =>
            await _service.LoginAsync(request);

        // Assert
        await act.Should()
            .ThrowAsync<ValidationException>();

        _userManagerMock.Verify(
            manager => manager.FindByEmailAsync(
                It.IsAny<string>()),
            Times.Never());
    }
}
