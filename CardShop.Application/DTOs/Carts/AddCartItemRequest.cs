namespace CardShop.Application.DTOs.Carts;

public class AddCartItemRequest
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }
}