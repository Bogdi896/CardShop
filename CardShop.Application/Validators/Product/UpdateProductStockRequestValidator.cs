using CardShop.Application.DTOs.Products;
using FluentValidation;

namespace CardShop.Application.Validators.Product
{
    public class UpdateProductStockRequestValidator
        : AbstractValidator<UpdateProductStockRequest>
    {
        public UpdateProductStockRequestValidator()
        {
            RuleFor(request => request.AvailableQuantity)
                .GreaterThanOrEqualTo(0);
        }
    }
}
