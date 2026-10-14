
using CardShop.Application.Common;
using CardShop.Application.DTOs.Orders;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;

namespace CardShop.Application.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByCheckoutIdAsync(
        string userId,
        Guid checkoutId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Order>> SearchAsync(
        SearchOrdersQuery query,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<Cart?> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task LockCartAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task LockOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<bool> DeductStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task RestoreStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<bool> SetOrderStatusAsync(
        int orderId,
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default);

    void Add(Order order);

    void ClearCart(Cart cart);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<IOrderTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
}

public interface IOrderTransaction : IAsyncDisposable
{
    Task CommitAsync(
        CancellationToken cancellationToken = default);
}
