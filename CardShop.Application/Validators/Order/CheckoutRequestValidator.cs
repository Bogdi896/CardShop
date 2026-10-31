using CardShop.Application.DTOs.Orders;
using FluentValidation;

namespace CardShop.Application.Validators.Orders;

public class CheckoutRequestValidator
    : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.CheckoutId)
            .NotEmpty();
    }
}