
using CardShop.Application.Common.Constants;
using CardShop.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CardShop.UnitTests.Authentication;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly JwtSettings _settings;

    private const string UserId = "user-123";
    private const string Email = "customer@example.com";

    public TokenServiceTests()
    {
        // Generate a random 64-byte signing key for testing.
        var secretKey = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));

        _settings = new JwtSettings
        {
            Issuer = "CardShop.Tests",
            Audience = "CardShop.TestClient",
            SecretKey = secretKey,
            ExpirationMinutes = 60
        };

        _tokenService = new TokenService(
            Options.Create(_settings));
    }

    // =========================================================
    // TOKEN GENERATION
    // =========================================================

    [Fact]
    public void GenerateToken_WithValidUser_ShouldReturnJwt()
    {
        // Arrange
        var roles = new List<string> { RoleNames.Customer };

        // Act
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            roles);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();

        handler.CanReadToken(result.AccessToken)
            .Should().BeTrue();
    }

    // =========================================================
    // USER INFORMATION
    // =========================================================

    [Fact]
    public void GenerateToken_ShouldContainCorrectUserId()
    {
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string> { RoleNames.Customer });

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Claims.Should().ContainSingle(
            claim => claim.Type == JwtRegisteredClaimNames.Sub &&
                     claim.Value == UserId);

        result.UserId.Should().Be(UserId);
    }

    [Fact]
    public void GenerateToken_ShouldContainCorrectEmail()
    {
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string> { RoleNames.Customer });

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Claims.Should().ContainSingle(
            claim => claim.Type == JwtRegisteredClaimNames.Email &&
                     claim.Value == Email);

        result.Email.Should().Be(Email);
    }

    // =========================================================
    // ROLE CLAIMS
    // =========================================================

    [Fact]
    public void GenerateToken_WithCustomerRole_ShouldContainCustomerRole()
    {
        var roles = new List<string> { RoleNames.Customer };

        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            roles);

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Claims.Should().Contain(
            claim => claim.Type == ClaimTypes.Role &&
                     claim.Value == RoleNames.Customer);

        result.Roles.Should().Contain(RoleNames.Customer);
    }

    [Fact]
    public void GenerateToken_WithAdminRole_ShouldContainAdminRole()
    {
        var roles = new List<string> { RoleNames.Admin };

        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            roles);

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Claims.Should().Contain(
            claim => claim.Type == ClaimTypes.Role &&
                     claim.Value == RoleNames.Admin);

        result.Roles.Should().Contain(RoleNames.Admin);
    }

    [Fact]
    public void GenerateToken_WithMultipleRoles_ShouldContainAllRoles()
    {
        var roles = new List<string>
        {
            RoleNames.Customer,
            RoleNames.Admin
        };

        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            roles);

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Claims.Should().Contain(
            claim => claim.Type == ClaimTypes.Role &&
                     claim.Value == RoleNames.Customer);

        token.Claims.Should().Contain(
            claim => claim.Type == ClaimTypes.Role &&
                     claim.Value == RoleNames.Admin);

        result.Roles.Should().BeEquivalentTo(roles);
    }

    // =========================================================
    // ISSUER AND AUDIENCE
    // =========================================================

    [Fact]
    public void GenerateToken_ShouldContainCorrectIssuer()
    {
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string>());

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Issuer.Should().Be(_settings.Issuer);
    }

    [Fact]
    public void GenerateToken_ShouldContainCorrectAudience()
    {
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string>());

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        token.Audiences.Should().Contain(_settings.Audience);
    }

    // =========================================================
    // EXPIRATION
    // =========================================================

    [Fact]
    public void GenerateToken_ShouldExpireAfterConfiguredDuration()
    {
        var beforeGeneration = DateTime.UtcNow;

        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string>());

        var afterGeneration = DateTime.UtcNow;

        result.ExpiresAtUtc.Should().BeOnOrAfter(
            beforeGeneration.AddMinutes(60));

        result.ExpiresAtUtc.Should().BeOnOrBefore(
            afterGeneration.AddMinutes(60));

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(result.AccessToken);

        // JWT expiration timestamps have second precision.
        token.ValidTo.Should().BeCloseTo(
            result.ExpiresAtUtc,
            TimeSpan.FromSeconds(1));
    }

    // =========================================================
    // TOKEN SIGNATURE
    // =========================================================

    [Fact]
    public void GenerateToken_ShouldHaveValidSignature()
    {
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string> { RoleNames.Customer });

        var key = new SymmetricSecurityKey(
            Convert.FromBase64String(_settings.SecretKey));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,

            ValidateAudience = true,
            ValidAudience = _settings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(
            result.AccessToken,
            validationParameters,
            out var validatedToken);

        principal.Should().NotBeNull();
        validatedToken.Should().BeOfType<JwtSecurityToken>();

        var jwt = (JwtSecurityToken)validatedToken;

        jwt.Header.Alg.Should().Be(
            SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public void GenerateToken_WithWrongSigningKey_ShouldFailValidation()
    {
        var result = _tokenService.GenerateToken(
            UserId,
            Email,
            new List<string> { RoleNames.Customer });

        // Generate a different signing key.
        var wrongKey = new SymmetricSecurityKey(
            RandomNumberGenerator.GetBytes(64));

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _settings.Issuer,

            ValidateAudience = true,
            ValidAudience = _settings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = wrongKey,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var handler = new JwtSecurityTokenHandler();

        Action act = () => handler.ValidateToken(
            result.AccessToken,
            validationParameters,
            out _);

        act.Should().Throw<SecurityTokenException>();
    }

    // =========================================================
    // UNIQUE TOKEN IDENTIFIERS
    // =========================================================

    [Fact]
    public void GenerateToken_CalledTwice_ShouldGenerateDifferentTokens()
    {
        var roles = new List<string> { RoleNames.Customer };

        var first = _tokenService.GenerateToken(
            UserId,
            Email,
            roles);

        var second = _tokenService.GenerateToken(
            UserId,
            Email,
            roles);

        first.AccessToken.Should().NotBe(second.AccessToken);

        var handler = new JwtSecurityTokenHandler();

        var firstToken = handler.ReadJwtToken(first.AccessToken);
        var secondToken = handler.ReadJwtToken(second.AccessToken);

        var firstJti = firstToken.Claims.Single(
            claim => claim.Type == JwtRegisteredClaimNames.Jti);

        var secondJti = secondToken.Claims.Single(
            claim => claim.Type == JwtRegisteredClaimNames.Jti);

        firstJti.Value.Should().NotBe(secondJti.Value);
    }
}
