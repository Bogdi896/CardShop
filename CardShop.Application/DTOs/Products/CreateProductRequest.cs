using CardShop.Domain.Enums;

namespace CardShop.Application.DTOs.Products
{
    public class CreateProductRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ProductType Type { get; set; }
        public decimal Price { get; set; }
        public int AvailableQuantity { get; set; }
    }
}