
using CardShop.Application.Interfaces.Repositories;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly CardShopDbContext _context;

    public CartRepository(CardShopDbContext context)
    {
        _context = context;
    }

    public async Task<Cart?> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .AsNoTracking()
            .Include(cart => cart.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(
                cart => cart.UserId == userId,
                cancellationToken);
    }

    public async Task<Cart?> GetForUpdateAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Carts
            .Include(cart => cart.Items)
                .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(
                cart => cart.UserId == userId,
                cancellationToken);
    }

    public async Task<Product?> GetProductByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .FirstOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);
    }

    public void Add(Cart cart)
    {
        _context.Carts.Add(cart);
    }

    public void RemoveItem(CartItem item)
    {
        _context.CartItems.Remove(item);
    }

    public void ClearItems(Cart cart)
    {
        _context.CartItems.RemoveRange(cart.Items);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
