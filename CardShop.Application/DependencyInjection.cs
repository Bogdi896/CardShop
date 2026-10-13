using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Services;
using CardShop.Application.Validators.Product;
using CardShop.Application.Validators.Auth;
using CardShop.Application.DTOs.Auth;
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

            services.AddScoped<
                IValidator<RegisterRequest>,
                RegisterRequestValidator>();

            services.AddScoped<
                IValidator<LoginRequest>,
                LoginRequestValidator>();

            return services;
        }
    }
}