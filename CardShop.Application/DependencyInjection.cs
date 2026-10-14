using CardShop.Application.DTOs.Auth;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Services;
using CardShop.Application.Validators.Auth;
using CardShop.Application.Validators.Carts;
using CardShop.Application.Validators.Orders;
using CardShop.Application.Validators.Product;
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
            services.AddScoped<ICartService, CartService>();

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

            services.AddScoped<
                IValidator<AddCartItemRequest>,
                AddCartItemRequestValidator>();

            services.AddScoped<
                IValidator<UpdateCartItemRequest>,
                UpdateCartItemRequestValidator>();

            services.AddScoped<IOrderService, OrderService>();

            services.AddScoped<
                IValidator<CheckoutRequest>,
                CheckoutRequestValidator>();

            services.AddScoped<
                IValidator<SearchOrdersQuery>,
                SearchOrdersQueryValidator>();

            return services;
        }
    }
}