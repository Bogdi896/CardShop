
using CardShop.Application.Common.Constants;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Interfaces.Services;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace CardShop.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly CardShopDbContext _context;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        CardShopDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _context = context;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        await _registerValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim()
        };

        var createResult = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!createResult.Succeeded)
        {
            throw new RegistrationException(
                createResult.Errors.Select(e => e.Description));
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user,
            RoleNames.Customer);

        if (!roleResult.Succeeded)
        {
            throw new RegistrationException(
                roleResult.Errors.Select(e => e.Description));
        }

        // Create the customer's permanent empty cart.
        var cart = new Cart
        {
            UserId = user.Id
        };

        _context.Carts.Add(cart);

        await _context.SaveChangesAsync(cancellationToken);

        // Commit user creation, role assignment and cart together.
        await transaction.CommitAsync(cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);

        return _tokenService.GenerateToken(
            user.Id,
            user.Email!,
            roles);
    }

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
