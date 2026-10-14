using CardShop.Application.DTOs.Carts;
using FluentValidation;

namespace CardShop.Application.Validators.Carts;

public class AddCartItemRequestValidator
    : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(request => request.ProductId)
            .GreaterThan(0);

        RuleFor(request => request.Quantity)
            .GreaterThan(0);
    }
}