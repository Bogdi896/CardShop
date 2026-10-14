using CardShop.Domain.Enums;

namespace CardShop.Application.DTOs.Orders;

public class OrderResponse
{
    public int Id { get; set; }

    public Guid CheckoutId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public IReadOnlyList<OrderItemResponse> Items { get; set; }
        = Array.Empty<OrderItemResponse>();
}