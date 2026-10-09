using CardShop.Domain.Enums;

namespace CardShop.Application.DTOs.Products
{
    public class SearchProductsQuery
    {
        public string? Search { get; set; }
        public ProductType? Type { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
