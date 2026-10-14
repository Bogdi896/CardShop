using CardShop.Application.DTOs.Orders;
using CardShop.Domain.Models;

namespace CardShop.Application.Mappings;

public static class OrderMapper
{
    public static OrderResponse ToDto(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            CheckoutId = order.CheckoutId,
            UserId = order.UserId,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            TotalAmount = order.TotalAmount,

            Items = order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemResponse
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                })
                .ToList()
        };
    }
}