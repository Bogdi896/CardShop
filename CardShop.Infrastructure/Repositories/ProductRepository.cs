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

        public async Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            _context.Products.Add(product);

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
