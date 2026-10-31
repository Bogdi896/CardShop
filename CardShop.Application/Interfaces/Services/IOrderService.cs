
using CardShop.Application.Common;
using CardShop.Application.DTOs.Orders;

namespace CardShop.Application.Interfaces.Services;

public interface IOrderService
{
    Task<OrderResponse> CheckoutAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderResponse?> GetByIdAsync(
        int id,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<OrderResponse>> SearchAsync(
        SearchOrdersQuery query,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<bool> ShipAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(
        int orderId,
        CancellationToken cancellationToken = default);
}
