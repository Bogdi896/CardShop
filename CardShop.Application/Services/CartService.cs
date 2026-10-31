
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Mappings;
using CardShop.Domain.Models;
using FluentValidation;

namespace CardShop.Application.Services;

public class CartService : ICartService
{
    private readonly ICartRepository _cartRepository;
    private readonly ICartOperationExecutor _executor;
    private readonly IValidator<AddCartItemRequest> _addValidator;
    private readonly IValidator<UpdateCartItemRequest> _updateValidator;

    public CartService(
        ICartRepository cartRepository,
        ICartOperationExecutor executor,
        IValidator<AddCartItemRequest> addValidator,
        IValidator<UpdateCartItemRequest> updateValidator)
    {
        _cartRepository = cartRepository;
        _executor = executor;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
    }

    // =========================================================
    // GET CART
    // =========================================================

    public async Task<CartResponse> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Read-only requests do not need the mutation executor.
        var cart = await _cartRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            throw new CartException(
                "Cart was not found for this user.");
        }

        return CartMapper.ToDto(cart);
    }

    // =========================================================
    // ADD ITEM
    // =========================================================

    public async Task<CartResponse> AddItemAsync(
        string userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await _addValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        return await _executor.ExecuteAsync(
            userId,
            async (repository, token) =>
            {
                var cart = await repository.GetForUpdateAsync(
                    userId,
                    token);

                if (cart is null)
                {
                    throw new CartException(
                        "Cart was not found for this user.");
                }

                var product = await repository.GetProductByIdAsync(
                    request.ProductId,
                    token);

                if (product is null || !product.IsActive)
                {
                    throw new CartException(
                        "Product is not available.");
                }

                var existingItem = cart.Items.FirstOrDefault(
                    item => item.ProductId == request.ProductId);

                var currentQuantity = existingItem?.Quantity ?? 0;

                var desiredQuantity =
                    (long)currentQuantity + request.Quantity;

                if (desiredQuantity > int.MaxValue)
                {
                    throw new CartException(
                        "Requested quantity is too large.");
                }

                if (desiredQuantity > product.AvailableQuantity)
                {
                    throw new CartException(
                        "Requested quantity exceeds available stock.");
                }

                if (existingItem is not null)
                {
                    existingItem.Quantity = (int)desiredQuantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = product.Id,
                        Product = product,
                        Quantity = request.Quantity
                    });
                }

                await repository.SaveChangesAsync(token);

                return CartMapper.ToDto(cart);
            },
            cancellationToken);
    }

    // =========================================================
    // UPDATE ITEM
    // =========================================================

    public async Task<CartResponse> UpdateItemAsync(
        string userId,
        int productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        return await _executor.ExecuteAsync(
            userId,
            async (repository, token) =>
            {
                var cart = await repository.GetForUpdateAsync(
                    userId,
                    token);

                if (cart is null)
                {
                    throw new CartException(
                        "Cart was not found for this user.");
                }

                var item = cart.Items.FirstOrDefault(
                    item => item.ProductId == productId);

                if (item is null)
                {
                    throw new CartException(
                        "Product is not in the cart.");
                }

                var product = await repository.GetProductByIdAsync(
                    productId,
                    token);

                if (product is null || !product.IsActive)
                {
                    throw new CartException(
                        "Product is not available.");
                }

                if (request.Quantity > product.AvailableQuantity)
                {
                    throw new CartException(
                        "Requested quantity exceeds available stock.");
                }

                item.Quantity = request.Quantity;

                await repository.SaveChangesAsync(token);

                return CartMapper.ToDto(cart);
            },
            cancellationToken);
    }

    // =========================================================
    // REMOVE ITEM
    // =========================================================

    public Task<bool> RemoveItemAsync(
        string userId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        return _executor.ExecuteAsync(
            userId,
            async (repository, token) =>
            {
                var cart = await repository.GetForUpdateAsync(
                    userId,
                    token);

                if (cart is null)
                {
                    throw new CartException(
                        "Cart was not found for this user.");
                }

                var item = cart.Items.FirstOrDefault(
                    item => item.ProductId == productId);

                if (item is null)
                {
                    return false;
                }

                repository.RemoveItem(item);

                await repository.SaveChangesAsync(token);

                return true;
            },
            cancellationToken);
    }

    // =========================================================
    // CLEAR CART
    // =========================================================

    public async Task ClearCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await _executor.ExecuteAsync(
            userId,
            async (repository, token) =>
            {
                var cart = await repository.GetForUpdateAsync(
                    userId,
                    token);

                if (cart is null)
                {
                    throw new CartException(
                        "Cart was not found for this user.");
                }

                repository.ClearItems(cart);

                await repository.SaveChangesAsync(token);

                return true;
            },
            cancellationToken);
    }
}
