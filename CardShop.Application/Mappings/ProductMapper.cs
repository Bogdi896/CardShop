using CardShop.Application.DTOs.Products;
using CardShop.Domain.Models;

namespace CardShop.Application.Mappings
{
    public static class ProductMapper
    {
        public static Product ToEntity(CreateProductRequest request)
        {
            return new Product
            {
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                Type = request.Type,
                Price = request.Price,
                AvailableQuantity = request.AvailableQuantity,
                IsActive = true
            };
        }

        public static ProductResponse ToDto(Product product)
        {
            return new ProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Type = product.Type,
                Price = product.Price,
                AvailableQuantity = product.AvailableQuantity,
                IsActive = product.IsActive
            };
        }
    }
}