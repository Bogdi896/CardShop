using CardShop.Application.Common;
using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Mappings;
using FluentValidation;

namespace CardShop.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IValidator<CreateProductRequest> _createValidator;
        private readonly IValidator<UpdateProductRequest> _updateValidator;
        private readonly IValidator<UpdateProductStockRequest> _stockValidator;
        private readonly IValidator<SearchProductsQuery> _searchValidator;

        public ProductService(
            IProductRepository productRepository,
            IValidator<CreateProductRequest> createValidator,
            IValidator<UpdateProductRequest> updateValidator,
            IValidator<UpdateProductStockRequest> stockValidator,
            IValidator<SearchProductsQuery> searchValidator)
        {
            _productRepository = productRepository;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
            _stockValidator = stockValidator;
            _searchValidator = searchValidator;
        }

        public async Task<ProductResponse> CreateAsync(
            CreateProductRequest request,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(
                request,
                cancellationToken);

            var product = ProductMapper.ToEntity(request);

            _productRepository.Add(product);

            await _productRepository.SaveChangesAsync(cancellationToken);

            return ProductMapper.ToDto(product);
        }

        public async Task<ProductResponse?> GetByIdAsync(
            int id,
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetByIdAsync(
                id,
                cancellationToken);

            if (product is null)
            {
                return null;
            }

            if (!includeInactive && !product.IsActive)
            {
                return null;
            }

            return ProductMapper.ToDto(product);
        }

        public async Task<PagedResult<ProductResponse>> SearchAsync(
            SearchProductsQuery query,
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            await _searchValidator.ValidateAndThrowAsync(
                query,
                cancellationToken);

            var result = await _productRepository.SearchAsync(
                query,
                includeInactive,
                cancellationToken);

            return new PagedResult<ProductResponse>
            {
                Items = result.Items
                    .Select(product => ProductMapper.ToDto(product))
                    .ToList(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }

        public async Task<ProductResponse?> UpdateAsync(
            int id,
            UpdateProductRequest request,
            CancellationToken cancellationToken = default)
        {
            await _updateValidator.ValidateAndThrowAsync(
                request,
                cancellationToken);

            var product = await _productRepository.GetForUpdateAsync(
                id,
                cancellationToken);

            if (product is null)
            {
                return null;
            }

            ProductMapper.ToEntity(request, product);

            await _productRepository.SaveChangesAsync(cancellationToken);

            return ProductMapper.ToDto(product);
        }

        public async Task<ProductResponse?> UpdateStockAsync(
            int id,
            UpdateProductStockRequest request,
            CancellationToken cancellationToken = default)
        {
            await _stockValidator.ValidateAndThrowAsync(
                request,
                cancellationToken);

            var product = await _productRepository.GetForUpdateAsync(
                id,
                cancellationToken);

            if (product is null)
            {
                return null;
            }

            product.AvailableQuantity = request.AvailableQuantity;

            await _productRepository.SaveChangesAsync(cancellationToken);

            return ProductMapper.ToDto(product);
        }

        public async Task<bool> SetActiveAsync(
            int id,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            var product = await _productRepository.GetForUpdateAsync(
                id,
                cancellationToken);

            if (product is null)
            {
                return false;
            }

            product.IsActive = isActive;

            await _productRepository.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}