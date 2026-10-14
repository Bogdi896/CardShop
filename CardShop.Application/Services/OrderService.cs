
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
    private readonly IValidator<CheckoutRequest> _checkoutValidator;
    private readonly IValidator<SearchOrdersQuery> _searchValidator;

    public OrderService(
        IOrderRepository orderRepository,
        IValidator<CheckoutRequest> checkoutValidator,
        IValidator<SearchOrdersQuery> searchValidator)
    {
        _orderRepository = orderRepository;
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

        // A completed checkout with the same key is returned
        // rather than processed again.
        var existingOrder =
            await _orderRepository.GetByCheckoutIdAsync(
                userId,
                request.CheckoutId,
                cancellationToken);

        if (existingOrder is not null)
        {
            return OrderMapper.ToDto(existingOrder);
        }

        await using var transaction =
            await _orderRepository.BeginTransactionAsync(
                cancellationToken);

        // Serialize competing checkouts for this user's cart.
        await _orderRepository.LockCartAsync(
            userId,
            cancellationToken);

        // Check again after acquiring the lock.
        existingOrder =
            await _orderRepository.GetByCheckoutIdAsync(
                userId,
                request.CheckoutId,
                cancellationToken);

        if (existingOrder is not null)
        {
            return OrderMapper.ToDto(existingOrder);
        }

        var cart = await _orderRepository.GetCartAsync(
            userId,
            cancellationToken);

        if (cart is null || !cart.Items.Any())
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

        // Always process products in a stable order to reduce
        // the likelihood of database deadlocks.
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

            // The database checks and deducts stock atomically.
            var deducted = await _orderRepository.DeductStockAsync(
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

        _orderRepository.Add(order);

        _orderRepository.ClearCart(cart);

        await _orderRepository.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

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

    public async Task<bool> ShipAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _orderRepository.BeginTransactionAsync(
                cancellationToken);

        await _orderRepository.LockOrderAsync(
            orderId,
            cancellationToken);

        var order = await _orderRepository.GetByIdAsync(
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

        var updated = await _orderRepository.SetOrderStatusAsync(
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

    public async Task<bool> CancelAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _orderRepository.BeginTransactionAsync(
                cancellationToken);

        await _orderRepository.LockOrderAsync(
            orderId,
            cancellationToken);

        var order = await _orderRepository.GetByIdAsync(
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

        var updated = await _orderRepository.SetOrderStatusAsync(
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
            await _orderRepository.RestoreStockAsync(
                item.ProductId,
                item.Quantity,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return true;
    }
}
