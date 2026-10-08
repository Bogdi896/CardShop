using CardShop.Application.Interfaces.Services;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.DTOs.Products;
using CardShop.Domain.Models;
using CardShop.Application.Mappings;
using FluentValidation;

namespace CardShop.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IValidator<CreateProductRequest> _validator;

        public ProductService(
            IProductRepository productRepository,
            IValidator<CreateProductRequest> validator)
        {
            _productRepository = productRepository;
            _validator = validator;
        }

        public async Task<ProductResponse> CreateProductAsync(
    CreateProductRequest request,
    CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(
                request,
                cancellationToken);

            var product = ProductMapper.ToEntity(request);

            await _productRepository.AddAsync(
                product,
                cancellationToken);

            return ProductMapper.ToDto(product);
        }

        public async Task<ProductResponse?> GetProductByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (product is null || !product.IsActive)
            {
                return null;
            }

            return ProductMapper.ToDto(product);
        }
    }
}