using CardShop.Application.Common;
using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace CardShop.Api.Controllers
{
    [ApiController]
    [Route("products")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ProductResponse>>> Search(
            [FromQuery] SearchProductsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await _productService.SearchAsync(
                query,
                includeInactive: false,
                cancellationToken: cancellationToken);

            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductResponse>> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var product = await _productService.GetByIdAsync(
                id,
                includeInactive: false,
                cancellationToken: cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }
    }
}