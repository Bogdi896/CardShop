using CardShop.Domain.Models;

namespace CardShop.Application.Interfaces.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<Cart?> GetForUpdateAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<Product?> GetProductByIdAsync(
        int productId,
        CancellationToken cancellationToken = default);

    void Add(Cart cart);

    void RemoveItem(CartItem item);

    void ClearItems(Cart cart);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}