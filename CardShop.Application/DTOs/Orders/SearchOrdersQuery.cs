using CardShop.Domain.Enums;

namespace CardShop.Application.DTOs.Orders;

public class SearchOrdersQuery
{
    public OrderStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}