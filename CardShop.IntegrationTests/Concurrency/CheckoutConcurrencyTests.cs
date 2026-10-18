
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.DTOs.Products;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Concurrency;

public class CheckoutConcurrencyTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }

    // =========================================================
    // HELPERS - AUTHENTICATION
    // =========================================================

    private static async Task<HttpClient> CreateCustomerAsync(
        SqlServerWebApplicationFactory factory)
    {
        var client = factory.CreateClient();

        var email = $"customer-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                Email = email,
                Password = "CardShop123!",
                ConfirmPassword = "CardShop123!"
            });

        response.EnsureSuccessStatusCode();

        var auth = await response.Content
            .ReadFromJsonAsync<AuthResponse>(JsonOptions);

        auth.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        return client;
    }

    private static async Task<HttpClient> CreateAdminAsync(
        SqlServerWebApplicationFactory factory)
    {
        var client = factory.CreateClient();

        var email = $"admin-{Guid.NewGuid():N}@example.com";
        const string password = "AdminPassword123!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(
                user,
                password);

            result.Succeeded.Should().BeTrue();

            var roleResult = await userManager.AddToRoleAsync(
                user,
                "Admin");

            roleResult.Succeeded.Should().BeTrue();
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest
            {
                Email = email,
                Password = password
            });

        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content
            .ReadFromJsonAsync<AuthResponse>(JsonOptions);

        auth.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        return client;
    }

    // =========================================================
    // HELPERS - PRODUCTS
    // =========================================================

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient admin,
        int stock)
    {
        var response = await admin.PostAsJsonAsync(
            "/admin/products",
            new CreateProductRequest
            {
                Name = $"Concurrency Product {Guid.NewGuid():N}",
                Description = "SQL Server concurrency test",
                Type = ProductType.BoosterBox,
                Price = 250m,
                AvailableQuantity = stock
            },
            JsonOptions);

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"the API returned: {body}");

        var product = await response.Content
            .ReadFromJsonAsync<ProductResponse>(JsonOptions);

        product.Should().NotBeNull();

        return product!;
    }

    // =========================================================
    // HELPERS - CART
    // =========================================================

    private static async Task AddToCartAsync(
        HttpClient customer,
        int productId,
        int quantity)
    {
        using var response = await customer.PostAsJsonAsync(
            "/cart/items",
            new AddCartItemRequest
            {
                ProductId = productId,
                Quantity = quantity
            });

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the API returned: {body}");
    }

    // =========================================================
    // HELPERS - CHECKOUT
    // =========================================================

    private static Task<HttpResponseMessage> CheckoutAsync(
        HttpClient customer,
        Guid checkoutId)
    {
        return customer.PostAsJsonAsync(
            "/orders/checkout",
            new CheckoutRequest
            {
                CheckoutId = checkoutId
            });
    }

    // =========================================================
    // HELPERS - DATABASE
    // =========================================================

    private static async Task<int> GetStockAsync(
        SqlServerWebApplicationFactory factory,
        int productId)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        return await context.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => product.AvailableQuantity)
            .SingleAsync();
    }

    private static async Task<List<Order>> GetOrdersForProductAsync(
        SqlServerWebApplicationFactory factory,
        int productId)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        return await context.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.Items.Any(
                item => item.ProductId == productId))
            .ToListAsync();
    }

    private static async Task<int> GetCartQuantityAsync(
        SqlServerWebApplicationFactory factory,
        string userId,
        int productId)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        return await context.CartItems
            .AsNoTracking()
            .Where(item =>
                item.Cart.UserId == userId &&
                item.ProductId == productId)
            .Select(item => item.Quantity)
            .SingleOrDefaultAsync();
    }

    // =========================================================
    // TEST 1: TWO CUSTOMERS BUY THE LAST PRODUCT
    // =========================================================

    [Fact]
    public async Task Checkout_TwoCustomersBuyingLastItem_ShouldNotOversell()
    {
        // Arrange
        using var factory = new SqlServerWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminAsync(factory);

        var product = await CreateProductAsync(
            admin,
            stock: 1);

        using var firstCustomer = await CreateCustomerAsync(factory);
        using var secondCustomer = await CreateCustomerAsync(factory);

        await AddToCartAsync(firstCustomer, product.Id, 1);
        await AddToCartAsync(secondCustomer, product.Id, 1);

        var firstCheckoutId = Guid.NewGuid();
        var secondCheckoutId = Guid.NewGuid();

        // Act - Send both requests concurrently.
        var firstTask = CheckoutAsync(
            firstCustomer,
            firstCheckoutId);

        var secondTask = CheckoutAsync(
            secondCustomer,
            secondCheckoutId);

        var responses = await Task.WhenAll(
            firstTask,
            secondTask);

        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        // Read both response bodies.
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        var statuses = responses
            .Select(response => response.StatusCode)
            .ToList();

        // Read final database state BEFORE assertions.
        var remainingStock = await GetStockAsync(
            factory,
            product.Id);

        var orders = await GetOrdersForProductAsync(
            factory,
            product.Id);

        var totalPurchasedQuantity = orders
            .SelectMany(order => order.Items)
            .Where(item => item.ProductId == product.Id)
            .Sum(item => item.Quantity);

        // Diagnostic information shown if an assertion fails.
        var diagnostics = $"""
            CONCURRENCY TEST DIAGNOSTICS

            First checkout:
              HTTP Status: {(int)firstResponse.StatusCode}
              Status Name: {firstResponse.StatusCode}
              Response Body: {firstBody}

            Second checkout:
              HTTP Status: {(int)secondResponse.StatusCode}
              Status Name: {secondResponse.StatusCode}
              Response Body: {secondBody}

            Database state:
              Initial Stock: 1
              Remaining Stock: {remainingStock}
              Orders Created: {orders.Count}
              Total Quantity Purchased: {totalPurchasedQuantity}
            """;

        // Assert - Exactly one checkout should succeed.
        statuses.Count(status => status == HttpStatusCode.OK)
            .Should().Be(1, diagnostics);

        // The other should receive a stock conflict.
        statuses.Count(status => status == HttpStatusCode.Conflict)
            .Should().Be(1, diagnostics);

        // Inventory must be zero, never negative.
        remainingStock.Should().Be(0, diagnostics);

        // Exactly one order should exist.
        orders.Should().ContainSingle(diagnostics);

        // Exactly one product unit should have been purchased.
        totalPurchasedQuantity.Should().Be(1, diagnostics);

        // The successful order should contain one unit.
        orders.Single().Items.Single().Quantity
            .Should().Be(1, diagnostics);
    }

    // =========================================================
    // TEST 2: SAME CHECKOUT SUBMITTED TWICE
    // =========================================================

    [Fact]
    public async Task Checkout_SameCheckoutIdSubmittedTwice_ShouldCreateOneOrder()
    {
        // Arrange
        using var factory = new SqlServerWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminAsync(factory);

        var product = await CreateProductAsync(
            admin,
            stock: 10);

        using var customer = await CreateCustomerAsync(factory);

        await AddToCartAsync(customer, product.Id, 2);

        var checkoutId = Guid.NewGuid();

        // Act - Send the same checkout request twice.
        var firstTask = CheckoutAsync(customer, checkoutId);
        var secondTask = CheckoutAsync(customer, checkoutId);

        var responses = await Task.WhenAll(
            firstTask,
            secondTask);

        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        // Read database state before assertions.
        var remainingStock = await GetStockAsync(
            factory,
            product.Id);

        var orders = await GetOrdersForProductAsync(
            factory,
            product.Id);

        var diagnostics = $"""
            IDEMPOTENCY TEST DIAGNOSTICS

            First checkout:
              HTTP Status: {(int)firstResponse.StatusCode}
              Response Body: {firstBody}

            Second checkout:
              HTTP Status: {(int)secondResponse.StatusCode}
              Response Body: {secondBody}

            Database state:
              Initial Stock: 10
              Remaining Stock: {remainingStock}
              Orders Created: {orders.Count}
              CheckoutId: {checkoutId}
            """;

        // Assert - Both requests should return HTTP 200.
        firstResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            diagnostics);

        secondResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            diagnostics);

        var firstOrder = JsonSerializer.Deserialize<OrderResponse>(
            firstBody,
            JsonOptions);

        var secondOrder = JsonSerializer.Deserialize<OrderResponse>(
            secondBody,
            JsonOptions);

        firstOrder.Should().NotBeNull();
        secondOrder.Should().NotBeNull();

        firstOrder!.Id.Should().Be(
            secondOrder!.Id,
            diagnostics);

        firstOrder.CheckoutId.Should().Be(
            checkoutId,
            diagnostics);

        // Exactly one order must be created.
        orders.Should().ContainSingle(diagnostics);

        // Stock must be deducted only once.
        remainingStock.Should().Be(8, diagnostics);
    }
}
