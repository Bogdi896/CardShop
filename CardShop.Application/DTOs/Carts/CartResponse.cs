namespace CardShop.Application.DTOs.Carts;

public class CartResponse
{
    public int Id { get; set; }

    public IReadOnlyList<CartItemResponse> Items { get; set; }
        = Array.Empty<CartItemResponse>();

    public decimal TotalPrice => Items.Sum(item => item.TotalPrice);

    public int TotalQuantity => Items.Sum(item => item.Quantity);
}