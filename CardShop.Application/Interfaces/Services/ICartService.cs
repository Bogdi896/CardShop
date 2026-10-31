using CardShop.Application.DTOs.Carts;

namespace CardShop.Application.Interfaces.Services;

public interface ICartService
{
    Task<CartResponse> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<CartResponse> AddItemAsync(
        string userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task<CartResponse> UpdateItemAsync(
        string userId,
        int productId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveItemAsync(
        string userId,
        int productId,
        CancellationToken cancellationToken = default);

    Task ClearCartAsync(
        string userId,
        CancellationToken cancellationToken = default);
}