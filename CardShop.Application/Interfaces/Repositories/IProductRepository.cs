using CardShop.Application.Common;
using CardShop.Application.DTOs.Products;
using CardShop.Domain.Models;

namespace CardShop.Application.Interfaces.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<Product?> GetForUpdateAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<PagedResult<Product>> SearchAsync(
            SearchProductsQuery query,
            bool includeInactive,
            CancellationToken cancellationToken = default);

        void Add(Product product);

        Task SaveChangesAsync(
            CancellationToken cancellationToken = default);
    }
}