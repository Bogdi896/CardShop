using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Services;
using CardShop.Application.Validators;
using CardShop.Application.Validators.Products;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services)
        {
            services.AddScoped<IProductService, ProductService>();

            services.AddScoped<
                IValidator<CreateProductRequest>,
                CreateProductRequestValidator>();

            services.AddScoped<
                IValidator<UpdateProductRequest>,
                UpdateProductRequestValidator>();

            services.AddScoped<
                IValidator<UpdateProductStockRequest>,
                UpdateProductStockRequestValidator>();

            services.AddScoped<
                IValidator<SearchProductsQuery>,
                SearchProductsQueryValidator>();

            return services;
        }
    }
}