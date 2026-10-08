using CardShop.Application.DTOs.Products;
using CardShop.Application.Validators.Products;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Services;

namespace CardShop.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services)
        {
            services.AddScoped<
                IValidator<CreateProductRequest>,
                CreateProductRequestValidator>();
            services.AddScoped<IProductService, ProductService>();

            // Register application services here as we create them.

            return services;
        }
    }
}