
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Validators.Auth;
using FluentAssertions;
using Xunit;

namespace CardShop.UnitTests.Validators;

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();

    private static RegisterRequest ValidRegisterRequest() => new()
    {
        RegistrationId = Guid.NewGuid(),
        Email = "customer@example.com",
        Password = "CardShop123!",
        ConfirmPassword = "CardShop123!"
    };

    private static LoginRequest ValidLoginRequest() => new()
    {
        Email = "customer@example.com",
        Password = "CardShop123!"
    };

    // =========================================================
    // REGISTRATION VALIDATION
    // =========================================================

    [Fact]
    public void Register_WithValidRequest_ShouldPass()
    {
        var request = ValidRegisterRequest();

        var result = _registerValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Register_WithEmptyRegistrationId_ShouldFail()
    {
        var request = ValidRegisterRequest();
        request.RegistrationId = Guid.Empty;

        var result = _registerValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName ==
                nameof(request.RegistrationId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    public void Register_WithInvalidEmail_ShouldFail(string? email)
    {
        var request = ValidRegisterRequest();
        request.Email = email!;

        var result = _registerValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Email));
    }

    [Fact]
    public void Register_WithEmailExceeding256Characters_ShouldFail()
    {
        var request = ValidRegisterRequest();
        request.Email = new string('a', 250) + "@example.com";

        var result = _registerValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Short1!")]
    public void Register_WithInvalidPassword_ShouldFail(string? password)
    {
        var request = ValidRegisterRequest();
        request.Password = password!;
        request.ConfirmPassword = password!;

        var result = _registerValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Password));
    }

    [Fact]
    public void Register_WithMismatchedPasswords_ShouldFail()
    {
        var request = ValidRegisterRequest();
        request.ConfirmPassword = "Different123!";

        var result = _registerValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName ==
                nameof(request.ConfirmPassword));
    }

    [Fact]
    public void Register_WithMatchingPasswords_ShouldPass()
    {
        var request = ValidRegisterRequest();

        var result = _registerValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    // =========================================================
    // LOGIN VALIDATION
    // =========================================================

    [Fact]
    public void Login_WithValidRequest_ShouldPass()
    {
        var request = ValidLoginRequest();

        var result = _loginValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid-email")]
    public void Login_WithInvalidEmail_ShouldFail(string? email)
    {
        var request = ValidLoginRequest();
        request.Email = email!;

        var result = _loginValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Login_WithEmptyPassword_ShouldFail(string? password)
    {
        var request = ValidLoginRequest();
        request.Password = password!;

        var result = _loginValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Password));
    }
}
