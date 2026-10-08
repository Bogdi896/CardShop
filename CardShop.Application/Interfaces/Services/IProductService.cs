using CardShop.Application.DTOs.Products;

namespace CardShop.Application.Interfaces.Services
{
    public interface IProductService
    {
        Task<ProductResponse> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    }
}
