using CardShop.Application.DTOs.Products;
using FluentValidation;

namespace CardShop.Application.Validators.Products
{
    public class CreateProductRequestValidator
        : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(request => request.Description)
                .NotNull()
                .MaximumLength(2000);

            RuleFor(request => request.Type)
                .IsInEnum();

            RuleFor(request => request.Price)
                .GreaterThan(0)
                .PrecisionScale(18, 2, true);

            RuleFor(request => request.AvailableQuantity)
                .GreaterThanOrEqualTo(0);
        }
    }
}