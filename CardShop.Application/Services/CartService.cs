
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
    private readonly IValidator<AddCartItemRequest> _addValidator;
    private readonly IValidator<UpdateCartItemRequest> _updateValidator;

    public CartService(
        ICartRepository cartRepository,
        IValidator<AddCartItemRequest> addValidator,
        IValidator<UpdateCartItemRequest> updateValidator)
    {
        _cartRepository = cartRepository;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
    }

    public async Task<CartResponse> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
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

    public async Task<CartResponse> AddItemAsync(
        string userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await _addValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        var cart = await _cartRepository.GetForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            throw new CartException(
                "Cart was not found for this user.");
        }

        var product = await _cartRepository.GetProductByIdAsync(
            request.ProductId,
            cancellationToken);

        if (product is null || !product.IsActive)
        {
            throw new CartException(
                "Product is not available.");
        }

        var existingItem = cart.Items.FirstOrDefault(
            item => item.ProductId == request.ProductId);

        // Calculate the desired total quantity.
        var currentQuantity = existingItem?.Quantity ?? 0;

        var desiredQuantity =
            (long)currentQuantity + request.Quantity;

        if (desiredQuantity > product.AvailableQuantity)
        {
            throw new CartException(
                "Requested quantity exceeds available stock.");
        }

        if (desiredQuantity > int.MaxValue)
        {
            throw new CartException(
                "Requested quantity is too large.");
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

        await _cartRepository.SaveChangesAsync(cancellationToken);

        return CartMapper.ToDto(cart);
    }

    public async Task<CartResponse> UpdateItemAsync(
        string userId,
        int productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(
            request,
            cancellationToken);

        var cart = await _cartRepository.GetForUpdateAsync(
            userId,
            cancellationToken);

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

        var product = await _cartRepository.GetProductByIdAsync(
            productId,
            cancellationToken);

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

        await _cartRepository.SaveChangesAsync(cancellationToken);

        return CartMapper.ToDto(cart);
    }

    public async Task<bool> RemoveItemAsync(
        string userId,
        int productId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetForUpdateAsync(
            userId,
            cancellationToken);

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

        _cartRepository.RemoveItem(item);

        await _cartRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task ClearCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            throw new CartException(
                "Cart was not found for this user.");
        }

        _cartRepository.ClearItems(cart);

        await _cartRepository.SaveChangesAsync(cancellationToken);
    }
}
