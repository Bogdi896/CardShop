
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
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Helpers;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Controllers;

public class OrderControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task<HttpClient> CreateCustomerClientAsync(
        CardShopWebApplicationFactory factory,
        string email = "customer@example.com")
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                RegistrationId = Guid.NewGuid(),
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

    private static async Task<HttpClient> CreateAdminClientAsync(
        CardShopWebApplicationFactory factory)
    {
        var client = factory.CreateClient();

        await AuthenticationHelper.AuthenticateAsAdminAsync(
            client,
            factory);

        return client;
    }

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient adminClient,
        string name = "Duel Masters Booster Box",
        decimal price = 250m,
        int stock = 10)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/admin/products",
            new CreateProductRequest
            {
                Name = name,
                Description = "Test product",
                Type = ProductType.BoosterBox,
                Price = price,
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

    private static async Task AddToCartAsync(
        HttpClient client,
        int productId,
        int quantity)
    {
        var response = await client.PostAsJsonAsync(
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

    private static async Task<HttpResponseMessage> CheckoutAsync(
        HttpClient client,
        Guid checkoutId)
    {
        return await client.PostAsJsonAsync(
            "/orders/checkout",
            new CheckoutRequest
            {
                CheckoutId = checkoutId
            });
    }

    private static async Task<OrderResponse> SuccessfulCheckoutAsync(
        HttpClient client,
        Guid? checkoutId = null)
    {
        using var response = await CheckoutAsync(
            client,
            checkoutId ?? Guid.NewGuid());

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"the API returned: {body}");

        var order = await response.Content
            .ReadFromJsonAsync<OrderResponse>(JsonOptions);

        order.Should().NotBeNull();

        return order!;
    }

    private static async Task<int> GetStockAsync(
        CardShopWebApplicationFactory factory,
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

    private static async Task<int> GetOrderCountAsync(
        CardShopWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        return await context.Orders.CountAsync();
    }

    // =========================================================
    // SUCCESSFUL CHECKOUT
    // =========================================================

    [Fact]
    public async Task Checkout_WithValidCart_ShouldCreateOrder()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 2);

        var checkoutId = Guid.NewGuid();

        var order = await SuccessfulCheckoutAsync(
            customer,
            checkoutId);

        order.Id.Should().BeGreaterThan(0);
        order.CheckoutId.Should().Be(checkoutId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(500m);

        order.Items.Should().ContainSingle();

        var item = order.Items.Single();

        item.ProductId.Should().Be(product.Id);
        item.ProductName.Should().Be(product.Name);
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(250m);

        (await GetOrderCountAsync(factory)).Should().Be(1);
    }

    [Fact]
    public async Task Checkout_ShouldDeductStockAndClearCart()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin, stock: 10);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 3);

        var cartBefore = await customer.GetFromJsonAsync<CartResponse>(
            "/cart");

        cartBefore.Should().NotBeNull();

        await SuccessfulCheckoutAsync(customer);

        (await GetStockAsync(factory, product.Id)).Should().Be(7);

        var cartAfter = await customer.GetFromJsonAsync<CartResponse>(
            "/cart");

        cartAfter.Should().NotBeNull();
        cartAfter!.Id.Should().Be(cartBefore!.Id);
        cartAfter.Items.Should().BeEmpty();
        cartAfter.TotalQuantity.Should().Be(0);
        cartAfter.TotalPrice.Should().Be(0);
    }

    [Fact]
    public async Task Checkout_WithMultipleProducts_ShouldCalculateTotal()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);

        var booster = await CreateProductAsync(
            admin,
            "Booster Box",
            250m,
            10);

        var deck = await CreateProductAsync(
            admin,
            "Starter Deck",
            80m,
            10);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, booster.Id, 2);
        await AddToCartAsync(customer, deck.Id, 3);

        var order = await SuccessfulCheckoutAsync(customer);

        order.Items.Should().HaveCount(2);
        order.TotalAmount.Should().Be(740m);

        (await GetStockAsync(factory, booster.Id)).Should().Be(8);
        (await GetStockAsync(factory, deck.Id)).Should().Be(7);
    }

    // =========================================================
    // CHECKOUT FAILURE
    // =========================================================

    [Fact]
    public async Task Checkout_WithEmptyCart_ShouldReturnConflict()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var customer = await CreateCustomerClientAsync(factory);

        using var response = await CheckoutAsync(
            customer,
            Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetOrderCountAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task Checkout_WithInsufficientStock_ShouldPreserveDatabase()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);

        var product = await CreateProductAsync(
            admin,
            stock: 5);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 5);

        // Simulate stock changing after the item was added.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            await context.Products
                .Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(
                        p => p.AvailableQuantity,
                        2));
        }

        using var response = await CheckoutAsync(
            customer,
            Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetOrderCountAsync(factory)).Should().Be(0);
        (await GetStockAsync(factory, product.Id)).Should().Be(2);

        var cart = await customer.GetFromJsonAsync<CartResponse>(
            "/cart");

        cart!.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Checkout_WithEmptyGuid_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var customer = await CreateCustomerClientAsync(factory);

        using var response = await CheckoutAsync(
            customer,
            Guid.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =========================================================
    // IDEMPOTENCY
    // =========================================================

    [Fact]
    public async Task Checkout_WithSameCheckoutId_ShouldReturnSameOrder()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 2);

        var checkoutId = Guid.NewGuid();

        var first = await SuccessfulCheckoutAsync(
            customer,
            checkoutId);

        var second = await SuccessfulCheckoutAsync(
            customer,
            checkoutId);

        second.Id.Should().Be(first.Id);
        second.CheckoutId.Should().Be(first.CheckoutId);

        (await GetOrderCountAsync(factory)).Should().Be(1);
        (await GetStockAsync(factory, product.Id)).Should().Be(8);
    }

    [Fact]
    public async Task Checkout_WithNewCheckoutIdAndEmptyCart_ShouldFail()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 1);

        await SuccessfulCheckoutAsync(customer);

        using var response = await CheckoutAsync(
            customer,
            Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetOrderCountAsync(factory)).Should().Be(1);
        (await GetStockAsync(factory, product.Id)).Should().Be(9);
    }

    // =========================================================
    // ORDER HISTORY AND OWNERSHIP
    // =========================================================

    [Fact]
    public async Task GetOrder_WithOwner_ShouldReturnOrder()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 1);

        var order = await SuccessfulCheckoutAsync(customer);

        using var response = await customer.GetAsync(
            $"/orders/{order.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var savedOrder = await response.Content
            .ReadFromJsonAsync<OrderResponse>(JsonOptions);

        savedOrder!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetOrder_WithAnotherUser_ShouldReturnNotFound()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var first = await CreateCustomerClientAsync(
            factory,
            "first@example.com");

        using var second = await CreateCustomerClientAsync(
            factory,
            "second@example.com");

        await AddToCartAsync(first, product.Id, 1);

        var order = await SuccessfulCheckoutAsync(first);

        using var response = await second.GetAsync(
            $"/orders/{order.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchOrders_ShouldOnlyReturnCurrentUsersOrders()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var first = await CreateCustomerClientAsync(
            factory,
            "first@example.com");

        using var second = await CreateCustomerClientAsync(
            factory,
            "second@example.com");

        await AddToCartAsync(first, product.Id, 1);
        var firstOrder = await SuccessfulCheckoutAsync(first);

        await AddToCartAsync(second, product.Id, 1);
        var secondOrder = await SuccessfulCheckoutAsync(second);

        // First customer's order history
        var firstHistory = await first.GetFromJsonAsync<
            CardShop.Application.Common.PagedResult<OrderResponse>>(
            "/orders",
            JsonOptions);

        firstHistory.Should().NotBeNull();
        firstHistory!.Items.Should().ContainSingle();
        firstHistory.Items.Single().Id.Should().Be(firstOrder.Id);

        // Second customer's order history
        var secondHistory = await second.GetFromJsonAsync<
            CardShop.Application.Common.PagedResult<OrderResponse>>(
            "/orders",
            JsonOptions);

        secondHistory.Should().NotBeNull();
        secondHistory!.Items.Should().ContainSingle();
        secondHistory.Items.Single().Id.Should().Be(secondOrder.Id);
    }

    // =========================================================
    // SHIPPING
    // =========================================================

    [Fact]
    public async Task Ship_PendingOrder_ShouldMarkAsShipped()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 2);
        var order = await SuccessfulCheckoutAsync(customer);

        using var response = await admin.PutAsync(
            $"/admin/orders/{order.Id}/ship",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var saved = await customer.GetFromJsonAsync<OrderResponse>(
            $"/orders/{order.Id}",
            JsonOptions);

        saved!.Status.Should().Be(OrderStatus.Shipped);

        (await GetStockAsync(factory, product.Id)).Should().Be(8);
    }

    // =========================================================
    // CANCELLATION
    // =========================================================

    [Fact]
    public async Task Cancel_PendingOrder_ShouldRestoreStock()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 3);
        var order = await SuccessfulCheckoutAsync(customer);

        (await GetStockAsync(factory, product.Id)).Should().Be(7);

        using var response = await admin.PutAsync(
            $"/admin/orders/{order.Id}/cancel",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetStockAsync(factory, product.Id)).Should().Be(10);

        var saved = await customer.GetFromJsonAsync<OrderResponse>(
            $"/orders/{order.Id}",
            JsonOptions);

        saved!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledOrder_ShouldNotRestoreStockTwice()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 2);
        var order = await SuccessfulCheckoutAsync(customer);

        using var first = await admin.PutAsync(
            $"/admin/orders/{order.Id}/cancel",
            null);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var second = await admin.PutAsync(
            $"/admin/orders/{order.Id}/cancel",
            null);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetStockAsync(factory, product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task Cancel_ShippedOrder_ShouldReturnConflict()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);
        var product = await CreateProductAsync(admin);

        using var customer = await CreateCustomerClientAsync(factory);

        await AddToCartAsync(customer, product.Id, 2);
        var order = await SuccessfulCheckoutAsync(customer);

        using var shipResponse = await admin.PutAsync(
            $"/admin/orders/{order.Id}/ship",
            null);

        shipResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var cancelResponse = await admin.PutAsync(
            $"/admin/orders/{order.Id}/cancel",
            null);

        cancelResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await GetStockAsync(factory, product.Id)).Should().Be(8);
    }

    // =========================================================
    // AUTHORIZATION
    // =========================================================

    [Fact]
    public async Task Checkout_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        using var response = await CheckoutAsync(
            client,
            Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminOrders_WithCustomerToken_ShouldReturnForbidden()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var customer = await CreateCustomerClientAsync(factory);

        using var response = await customer.GetAsync(
            "/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminOrders_WithAdminToken_ShouldReturnOk()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminClientAsync(factory);

        using var response = await admin.GetAsync(
            "/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
