using CardShop.Application.DTOs.Carts;
using CardShop.Domain.Models;

namespace CardShop.Application.Mappings;

public static class CartMapper
{
    public static CartResponse ToDto(Cart cart)
    {
        return new CartResponse
        {
            Id = cart.Id,

            Items = cart.Items
                .OrderBy(item => item.ProductId)
                .Select(item => new CartItemResponse
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    UnitPrice = item.Product.Price,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }
}