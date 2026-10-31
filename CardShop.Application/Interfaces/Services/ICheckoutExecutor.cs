
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.DTOs.Orders;

namespace CardShop.Application.Interfaces.Services;

public interface ICheckoutExecutor
{
    Task<T> ExecuteAsync<T>(
        Func<IOrderRepository, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> ExecuteCheckoutAsync(
        string userId,
        Guid checkoutId,
        Func<IOrderRepository, CancellationToken, Task<OrderResponse>> operation,
        CancellationToken cancellationToken = default);
}
