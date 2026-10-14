using CardShop.Application.DTOs.Carts;
using FluentValidation;

namespace CardShop.Application.Validators.Carts;

public class UpdateCartItemRequestValidator
    : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(request => request.Quantity)
            .GreaterThan(0);
    }
}