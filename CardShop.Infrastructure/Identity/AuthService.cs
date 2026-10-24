
using CardShop.Application.Common.Constants;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Interfaces.Services;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly CardShopDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        CardShopDbContext context,
        IServiceScopeFactory scopeFactory)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _context = context;
        _scopeFactory = scopeFactory;
    }

    // =========================================================
    // REGISTRATION
    // =========================================================

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        var email = request.Email.Trim();
        var registrationId = request.RegistrationId;

        // Check for an already completed registration.
        // Do not issue a JWT based on RegistrationId alone.
        var existingRegistration = await _context.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.RegistrationId == registrationId,
                cancellationToken);

        if (existingRegistration)
        {
            throw new RegistrationException(new[]
            {
                "This registration was already completed. Please log in."
            });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Each retry gets fresh EF Core and Identity services.
            await using var scope = _scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var tokenService = scope.ServiceProvider
                .GetRequiredService<ITokenService>();

            // Recheck because another attempt may have completed.
            var alreadyRegistered = await context.Users
                .AsNoTracking()
                .AnyAsync(
                    user => user.RegistrationId == registrationId,
                    cancellationToken);

            if (alreadyRegistered)
            {
                throw new RegistrationException(new[]
                {
                    "This registration was already completed. Please log in."
                });
            }

            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    cancellationToken);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                RegistrationId = registrationId
            };

            var createResult = await userManager.CreateAsync(
                user,
                request.Password);

            if (!createResult.Succeeded)
            {
                throw new RegistrationException(
                    createResult.Errors.Select(
                        error => error.Description));
            }

            var roleResult = await userManager.AddToRoleAsync(
                user,
                RoleNames.Customer);

            if (!roleResult.Succeeded)
            {
                throw new RegistrationException(
                    roleResult.Errors.Select(
                        error => error.Description));
            }

            // Each user receives one permanent empty cart.
            context.Carts.Add(new Cart
            {
                UserId = user.Id
            });

            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception) when (
                !cancellationToken.IsCancellationRequested &&
                CardShop.Infrastructure.Services
                    .TransactionFailureClassifier
                    .IsPotentialCommitFailure(exception))
            {
                // The commit may have succeeded even if the
                // application did not receive confirmation.
                await using var verificationScope =
                    _scopeFactory.CreateAsyncScope();

                var verificationContext =
                    verificationScope.ServiceProvider
                        .GetRequiredService<CardShopDbContext>();

                var committed = await verificationContext.Users
                    .AsNoTracking()
                    .AnyAsync(
                        candidate =>
                            candidate.Id == user.Id &&
                            candidate.RegistrationId == registrationId,
                        cancellationToken);

                if (committed)
                {
                    // Do not generate a JWT merely from the
                    // registration identifier.
                    throw new RegistrationException(new[]
                    {
                        "Registration completed, but confirmation was lost. " +
                        "Please log in."
                    });
                }

                // No committed registration was found.
                // Let the execution strategy decide whether
                // the original failure should be retried.
                throw;
            }

            var roles = await userManager.GetRolesAsync(user);

            return tokenService.GenerateToken(
                user.Id,
                user.Email!,
                roles);
        });
    }

    // =========================================================
    // LOGIN
    // =========================================================

    public async Task<AuthResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return null;
        }

        var signInResult =
            await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);

        return _tokenService.GenerateToken(
            user.Id,
            user.Email!,
            roles);
    }
}
