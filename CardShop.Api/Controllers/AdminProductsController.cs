using CardShop.Application.Common;
using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace CardShop.Api.Controllers
{
    [ApiController]
    [Route("admin/products")]
    public class AdminProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public AdminProductsController(IProductService productService)
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
                includeInactive: true,
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
                includeInactive: true,
                cancellationToken: cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        [HttpPost]
        public async Task<ActionResult<ProductResponse>> Create(
            [FromBody] CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            var product = await _productService.CreateAsync(
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                product);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ProductResponse>> Update(
            int id,
            [FromBody] UpdateProductRequest request,
            CancellationToken cancellationToken)
        {
            var product = await _productService.UpdateAsync(
                id,
                request,
                cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        [HttpPut("{id:int}/stock")]
        public async Task<ActionResult<ProductResponse>> UpdateStock(
            int id,
            [FromBody] UpdateProductStockRequest request,
            CancellationToken cancellationToken)
        {
            var product = await _productService.UpdateStockAsync(
                id,
                request,
                cancellationToken);

            if (product is null)
            {
                return NotFound();
            }

            return Ok(product);
        }

        [HttpPut("{id:int}/activate")]
        public async Task<IActionResult> Activate(
            int id,
            CancellationToken cancellationToken)
        {
            var found = await _productService.SetActiveAsync(
                id,
                isActive: true,
                cancellationToken: cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPut("{id:int}/deactivate")]
        public async Task<IActionResult> Deactivate(
            int id,
            CancellationToken cancellationToken)
        {
            var found = await _productService.SetActiveAsync(
                id,
                isActive: false,
                cancellationToken: cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}