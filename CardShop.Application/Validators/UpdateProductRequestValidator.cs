using CardShop.Application.DTOs.Products;
using FluentValidation;

namespace CardShop.Application.Validators
{
    public class UpdateProductRequestValidator
        : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
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
        }
    }
}
