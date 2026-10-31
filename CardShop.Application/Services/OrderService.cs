
using CardShop.Application.Common;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Mappings;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;
using FluentValidation;

namespace CardShop.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICheckoutExecutor _checkoutExecutor;
    private readonly IValidator<CheckoutRequest> _checkoutValidator;
    private readonly IValidator<SearchOrdersQuery> _searchValidator;

    public OrderService(
        IOrderRepository orderRepository,
        ICheckoutExecutor checkoutExecutor,
        IValidator<CheckoutRequest> checkoutValidator,
        IValidator<SearchOrdersQuery> searchValidator)
    {
        _orderRepository = orderRepository;
        _checkoutExecutor = checkoutExecutor;
        _checkoutValidator = checkoutValidator;
        _searchValidator = searchValidator;
    }

    public async Task<OrderResponse> CheckoutAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        await _checkoutValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        return await _checkoutExecutor.ExecuteCheckoutAsync(
            userId,
            request.CheckoutId,
            (repository, token) => CheckoutCoreAsync(
                repository,
                userId,
                request,
                token),
            cancellationToken);
    }

    private static async Task<OrderResponse> CheckoutCoreAsync(
        IOrderRepository repository,
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        await repository.LockCartAsync(
            userId,
            cancellationToken);

        var existingOrder = await repository.GetByCheckoutIdAsync(
            userId,
            request.CheckoutId,
            cancellationToken);

        if (existingOrder is not null)
        {
            return OrderMapper.ToDto(existingOrder);
        }

        var cart = await repository.GetCartAsync(
            userId,
            cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            throw new OrderException(
                "Cannot checkout an empty cart.");
        }

        var order = new Order
        {
            UserId = userId,
            CheckoutId = request.CheckoutId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var cartItem in cart.Items.OrderBy(
            item => item.ProductId))
        {
            var product = cartItem.Product;

            if (product is null ||
                !product.IsActive ||
                cartItem.Quantity <= 0)
            {
                throw new OrderException(
                    "The cart contains an unavailable product.");
            }

            var deducted = await repository.DeductStockAsync(
                product.Id,
                cartItem.Quantity,
                cancellationToken);

            if (!deducted)
            {
                throw new OrderException(
                    $"Insufficient stock for '{product.Name}'.");
            }

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = cartItem.Quantity,
                UnitPrice = product.Price
            });
        }

        order.TotalAmount = order.Items.Sum(
            item => item.UnitPrice * item.Quantity);

        repository.Add(order);
        repository.ClearCart(cart);

        await repository.SaveChangesAsync(cancellationToken);

        return OrderMapper.ToDto(order);
    }

    public async Task<OrderResponse?> GetByIdAsync(
        int id,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(
            id,
            cancellationToken);

        if (order is null)
        {
            return null;
        }

        if (userId is not null && order.UserId != userId)
        {
            return null;
        }

        return OrderMapper.ToDto(order);
    }

    public async Task<PagedResult<OrderResponse>> SearchAsync(
        SearchOrdersQuery query,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        await _searchValidator.ValidateAndThrowAsync(
            query,
            cancellationToken);

        var result = await _orderRepository.SearchAsync(
            query,
            userId,
            cancellationToken);

        return new PagedResult<OrderResponse>
        {
            Items = result.Items
                .Select(OrderMapper.ToDto)
                .ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public Task<bool> ShipAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return _checkoutExecutor.ExecuteAsync(
            (repository, token) => ShipCoreAsync(
                repository,
                orderId,
                token),
            cancellationToken);
    }

    private static async Task<bool> ShipCoreAsync(
        IOrderRepository repository,
        int orderId,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await repository.BeginTransactionAsync(cancellationToken);

        await repository.LockOrderAsync(orderId, cancellationToken);

        var order = await repository.GetByIdAsync(
            orderId,
            cancellationToken);

        if (order is null)
        {
            return false;
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new OrderException(
                "Only pending orders can be shipped.");
        }

        var updated = await repository.SetOrderStatusAsync(
            orderId,
            OrderStatus.Pending,
            OrderStatus.Shipped,
            cancellationToken);

        if (!updated)
        {
            throw new OrderException(
                "Order status changed during processing.");
        }

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    public Task<bool> CancelAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return _checkoutExecutor.ExecuteAsync(
            (repository, token) => CancelCoreAsync(
                repository,
                orderId,
                token),
            cancellationToken);
    }

    private static async Task<bool> CancelCoreAsync(
        IOrderRepository repository,
        int orderId,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await repository.BeginTransactionAsync(cancellationToken);

        await repository.LockOrderAsync(orderId, cancellationToken);

        var order = await repository.GetByIdAsync(
            orderId,
            cancellationToken);

        if (order is null)
        {
            return false;
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new OrderException(
                "Only pending orders can be cancelled.");
        }

        var updated = await repository.SetOrderStatusAsync(
            orderId,
            OrderStatus.Pending,
            OrderStatus.Cancelled,
            cancellationToken);

        if (!updated)
        {
            throw new OrderException(
                "Order status changed during processing.");
        }

        foreach (var item in order.Items.OrderBy(
            item => item.ProductId))
        {
            await repository.RestoreStockAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}
