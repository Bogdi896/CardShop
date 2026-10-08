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

        [HttpPost]
        public async Task<ActionResult<ProductResponse>> Create(
            [FromBody] CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            var product = await _productService.CreateProductAsync(
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                product);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductResponse>> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var product = await _productService.GetProductByIdAsync(
                id,
                cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }
    }
}