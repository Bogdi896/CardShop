using CardShop.Application.DTOs.Orders;
using FluentValidation;

namespace CardShop.Application.Validators.Orders;

public class SearchOrdersQueryValidator
    : AbstractValidator<SearchOrdersQuery>
{
    public SearchOrdersQueryValidator()
    {
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);

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