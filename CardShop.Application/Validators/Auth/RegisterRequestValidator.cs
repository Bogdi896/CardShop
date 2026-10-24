
using CardShop.Application.DTOs.Auth;
using FluentValidation;

namespace CardShop.Application.Validators.Auth;

public class RegisterRequestValidator
    : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.RegistrationId)
            .NotEmpty()
            .WithMessage("RegistrationId is required.");

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]")
            .WithMessage(
                "Password must contain an uppercase letter.")
            .Matches("[a-z]")
            .WithMessage(
                "Password must contain a lowercase letter.")
            .Matches("[0-9]")
            .WithMessage(
                "Password must contain a number.")
            .Matches("[^a-zA-Z0-9]")
            .WithMessage(
                "Password must contain a special character.");

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password)
            .WithMessage("Passwords do not match.");
    }
}
