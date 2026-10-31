
using System.Data;
using CardShop.Application.Common;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CardShop.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly CardShopDbContext _context;

    public OrderRepository(CardShopDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // GET ORDER BY ID
    // =========================================================

    public async Task<Order?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == id,
                cancellationToken);
    }

    // =========================================================
    // GET ORDER BY CHECKOUT ID
    // =========================================================

    public async Task<Order?> GetByCheckoutIdAsync(
        string userId,
        Guid checkoutId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.UserId == userId &&
                         order.CheckoutId == checkoutId,
                cancellationToken);
    }

    // =========================================================
    // SEARCH ORDERS
    // =========================================================

    public async Task<PagedResult<Order>> SearchAsync(
        SearchOrdersQuery query,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Order> orders = _context.Orders
            .AsNoTracking();

        if (userId is not null)
        {
            orders = orders.Where(order =>
                order.UserId == userId);
        }

        if (query.Status.HasValue)
        {
            orders = orders.Where(order =>
                order.Status == query.Status.Value);
        }

        var totalCount = await orders.CountAsync(
            cancellationToken);

        var skip = checked(
            (query.Page - 1) * query.PageSize);

        List<Order> items;

        // SQLite does not support DateTimeOffset ordering
        // in the same way SQL Server does.
        if (_context.Database.ProviderName ==
            "Microsoft.EntityFrameworkCore.Sqlite")
        {
            items = await orders
                .Include(order => order.Items)
                .OrderByDescending(order => order.Id)
                .Skip(skip)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            items = await orders
                .Include(order => order.Items)
                .OrderByDescending(order => order.CreatedAt)
                .ThenByDescending(order => order.Id)
                .Skip(skip)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
        }

        return new PagedResult<Order>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    // =========================================================
    // GET CUSTOMER CART
    // =========================================================

    public async Task<Cart?> GetCartAsync(
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

    // =========================================================
    // LOCK CART FOR CHECKOUT
    // =========================================================

    public async Task LockCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsSqlServer())
        {
            var cart = await _context.Carts
                .FromSqlInterpolated(
                    $@"SELECT * FROM Carts WITH (UPDLOCK, HOLDLOCK)
                       WHERE UserId = {userId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (cart is null)
            {
                throw new InvalidOperationException(
                    "Cart does not exist.");
            }
        }
        else
        {
            // SQLite fallback for integration tests.
            var exists = await _context.Carts
                .AnyAsync(
                    cart => cart.UserId == userId,
                    cancellationToken);

            if (!exists)
            {
                throw new InvalidOperationException(
                    "Cart does not exist.");
            }
        }
    }

    // =========================================================
    // LOCK ORDER FOR STATUS CHANGES
    // =========================================================

    public async Task LockOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.IsSqlServer())
        {
            var order = await _context.Orders
                .FromSqlInterpolated(
                    $@"SELECT * FROM Orders WITH (UPDLOCK, HOLDLOCK)
                       WHERE Id = {orderId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                throw new InvalidOperationException(
                    "Order does not exist.");
            }
        }
    }

    // =========================================================
    // ATOMIC STOCK DEDUCTION
    // =========================================================

    public async Task<bool> DeductStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.Products
            .Where(product =>
                product.Id == productId &&
                product.IsActive &&
                product.AvailableQuantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    product => product.AvailableQuantity,
                    product => product.AvailableQuantity - quantity),
                cancellationToken);

        return affectedRows == 1;
    }

    // =========================================================
    // RESTORE STOCK AFTER CANCELLATION
    // =========================================================

    public async Task RestoreStockAsync(
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.Products
            .Where(product => product.Id == productId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    product => product.AvailableQuantity,
                    product => product.AvailableQuantity + quantity),
                cancellationToken);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Could not restore stock for product {productId}.");
        }
    }

    // =========================================================
    // UPDATE ORDER STATUS
    // =========================================================

    public async Task<bool> SetOrderStatusAsync(
        int orderId,
        OrderStatus expectedStatus,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _context.Orders
            .Where(order =>
                order.Id == orderId &&
                order.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    order => order.Status,
                    newStatus),
                cancellationToken);

        return affectedRows == 1;
    }

    // =========================================================
    // ADD ORDER
    // =========================================================

    public void Add(Order order)
    {
        _context.Orders.Add(order);
    }

    // =========================================================
    // CLEAR CART
    // =========================================================

    public void ClearCart(Cart cart)
    {
        var itemIds = cart.Items
            .Select(item => item.Id)
            .ToList();

        _context.CartItems.RemoveRange(
            _context.CartItems.Where(item =>
                itemIds.Contains(item.Id)));
    }

    // =========================================================
    // SAVE CHANGES
    // =========================================================

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    // =========================================================
    // BEGIN DATABASE TRANSACTION
    // =========================================================

    public async Task<IOrderTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database
            .BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        return new OrderTransaction(transaction);
    }

    // =========================================================
    // TRANSACTION WRAPPER
    // =========================================================

    private sealed class OrderTransaction : IOrderTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public OrderTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(
            CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
