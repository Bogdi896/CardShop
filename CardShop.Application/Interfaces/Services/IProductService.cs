using CardShop.Application.Common;
using CardShop.Application.DTOs.Products;

namespace CardShop.Application.Interfaces.Services
{
    public interface IProductService
    {
        Task<ProductResponse> CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default);

        Task<ProductResponse?> GetByIdAsync(
            int id,
            bool includeInactive = false,
            CancellationToken cancellationToken = default);

        Task<PagedResult<ProductResponse>> SearchAsync(
            SearchProductsQuery query,
            bool includeInactive = false,
            CancellationToken cancellationToken = default);

        Task<ProductResponse?> UpdateAsync(
            int id,
            UpdateProductRequest request,
            CancellationToken cancellationToken = default);

        Task<ProductResponse?> UpdateStockAsync(
            int id,
            UpdateProductStockRequest request,
            CancellationToken cancellationToken = default);

        Task<bool> SetActiveAsync(
            int id,
            bool isActive,
            CancellationToken cancellationToken = default);
    }
}