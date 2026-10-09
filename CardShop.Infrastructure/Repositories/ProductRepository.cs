using CardShop.Application.Common;
using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CardShop.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly CardShopDbContext _context;

        public ProductRepository(CardShopDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    product => product.Id == id,
                    cancellationToken);
        }

        public async Task<Product?> GetForUpdateAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Products
                .FirstOrDefaultAsync(
                    product => product.Id == id,
                    cancellationToken);
        }

        public async Task<PagedResult<Product>> SearchAsync(
            SearchProductsQuery query,
            bool includeInactive,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Product> products = _context.Products
                .AsNoTracking();

            if (!includeInactive)
            {
                products = products.Where(product => product.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                products = products.Where(product =>
                    product.Name.Contains(search));
            }

            if (query.Type.HasValue)
            {
                products = products.Where(product =>
                    product.Type == query.Type.Value);
            }

            if (query.MinPrice.HasValue)
            {
                products = products.Where(product =>
                    product.Price >= query.MinPrice.Value);
            }

            if (query.MaxPrice.HasValue)
            {
                products = products.Where(product =>
                    product.Price <= query.MaxPrice.Value);
            }

            var totalCount = await products.CountAsync(cancellationToken);

            var skip = checked((query.Page - 1) * query.PageSize);

            var items = await products
                .OrderBy(product => product.Id)
                .Skip(skip)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Product>
            {
                Items = items,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public void Add(Product product)
        {
            _context.Products.Add(product);
        }

        public async Task SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}