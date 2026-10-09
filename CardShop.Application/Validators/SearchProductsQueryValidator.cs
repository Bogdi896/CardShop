
using CardShop.Application.DTOs.Products;
using FluentValidation;

namespace CardShop.Application.Validators
{
    public class SearchProductsQueryValidator
        : AbstractValidator<SearchProductsQuery>
    {
        public SearchProductsQueryValidator()
        {
            RuleFor(query => query.Search)
                .MaximumLength(200);

            RuleFor(query => query.Type)
                .IsInEnum()
                .When(query => query.Type.HasValue);

            RuleFor(query => query.MinPrice)
                .GreaterThanOrEqualTo(0m)
                .When(query => query.MinPrice.HasValue);

            RuleFor(query => query.MaxPrice)
                .GreaterThanOrEqualTo(0m)
                .When(query => query.MaxPrice.HasValue);

            RuleFor(query => query.MaxPrice)
                .Must((query, maxPrice) =>
                    maxPrice!.Value >= query.MinPrice!.Value)
                .When(query =>
                    query.MinPrice.HasValue &&
                    query.MaxPrice.HasValue)
                .WithMessage(
                    "Maximum price must be greater than or equal to minimum price.");

            RuleFor(query => query.Page)
                .GreaterThanOrEqualTo(1);

            RuleFor(query => query.PageSize)
                .InclusiveBetween(1, 100);

            RuleFor(query => query.Page)
                .Must((query, page) =>
                    ((long)page - 1) * query.PageSize <= int.MaxValue)
                .When(query => query.Page >= 1 && query.PageSize >= 1)
                .WithMessage("The requested page is too large.");
        }
    }
}
