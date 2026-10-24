
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.DTOs.Products;
using CardShop.Domain.Enums;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Helpers;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace CardShop.IntegrationTests.Controllers;

public class CartControllerTests
{
    private static async Task<HttpClient> CreateCustomerClientAsync(
        CardShopWebApplicationFactory factory,
        string email = "customer@example.com")
    {
        var client = factory.CreateClient();

        var registration = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                RegistrationId = Guid.NewGuid(),
                Email = email,
                Password = "CardShop123!",
                ConfirmPassword = "CardShop123!"
            });

        registration.EnsureSuccessStatusCode();

        var auth = await registration.Content
            .ReadFromJsonAsync<AuthResponse>();

        auth.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        return client;
    }


    private static async Task<ProductResponse> CreateProductAsync(
        CardShopWebApplicationFactory factory,
        int stock = 10,
        bool isActive = true)
    {
        using var adminClient = factory.CreateClient();

        await AuthenticationHelper.AuthenticateAsAdminAsync(
            adminClient,
            factory);

        var request = new CreateProductRequest
        {
            Name = "Duel Masters Booster Box",
            Description = "Japanese booster box",
            Type = ProductType.BoosterBox,
            Price = 250m,
            AvailableQuantity = stock
        };

        // Serialize enums as strings, matching the API configuration.
        var jsonOptions = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        jsonOptions.Converters.Add(
            new JsonStringEnumConverter());

        var response = await adminClient.PostAsJsonAsync(
            "/admin/products",
            request,
            jsonOptions);

        // Include the API response body if the assertion fails.
        var responseBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"the API returned: {responseBody}");

        var product = await response.Content
            .ReadFromJsonAsync<ProductResponse>(jsonOptions);

        product.Should().NotBeNull();

        if (!isActive)
        {
            var deactivateResponse = await adminClient.PutAsync(
                $"/admin/products/{product!.Id}/deactivate",
                null);

            deactivateResponse.StatusCode.Should()
                .Be(HttpStatusCode.NoContent);
        }

        return product!;
    }


    private static async Task<CartResponse> GetCartAsync(
        HttpClient client)
    {
        var response = await client.GetAsync("/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await response.Content
            .ReadFromJsonAsync<CartResponse>();

        cart.Should().NotBeNull();

        return cart!;
    }

    private static async Task<CartResponse> AddItemAsync(
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await response.Content
            .ReadFromJsonAsync<CartResponse>();

        cart.Should().NotBeNull();

        return cart!;
    }

    // =========================================================
    // CART CREATION
    // =========================================================

    [Fact]
    public async Task Register_ShouldAutomaticallyCreateEmptyCart()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = await CreateCustomerClientAsync(factory);

        var cart = await GetCartAsync(client);

        cart.Id.Should().BeGreaterThan(0);
        cart.Items.Should().BeEmpty();
        cart.TotalQuantity.Should().Be(0);
        cart.TotalPrice.Should().Be(0);
    }

    // =========================================================
    // AUTHENTICATION
    // =========================================================

    [Fact]
    public async Task GetCart_WithoutToken_ShouldReturnUnauthorized()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/cart");

        response.StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCart_WithAuthenticatedUser_ShouldReturnOk()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = await CreateCustomerClientAsync(factory);

        var response = await client.GetAsync("/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================================
    // ADD ITEMS
    // =========================================================

    [Fact]
    public async Task AddItem_WithValidProduct_ShouldPersistItem()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        var result = await AddItemAsync(client, product.Id, 2);

        result.Items.Should().ContainSingle();
        result.TotalQuantity.Should().Be(2);
        result.TotalPrice.Should().Be(500m);

        var savedCart = await GetCartAsync(client);

        savedCart.Items.Should().ContainSingle();
        savedCart.Items.Single().ProductId.Should().Be(product.Id);
        savedCart.Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task AddItem_WhenProductAlreadyInCart_ShouldIncreaseQuantity()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        await AddItemAsync(client, product.Id, 2);
        await AddItemAsync(client, product.Id, 3);

        var cart = await GetCartAsync(client);

        cart.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(5);
        cart.TotalPrice.Should().Be(1250m);
    }

    [Fact]
    public async Task AddItem_WhenProductInactive_ShouldReturnConflict()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(
            factory,
            isActive: false);

        using var client = await CreateCustomerClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/cart/items",
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 1
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var cart = await GetCartAsync(client);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddItem_WhenQuantityExceedsStock_ShouldReturnConflict()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory, stock: 3);

        using var client = await CreateCustomerClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/cart/items",
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 5
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var cart = await GetCartAsync(client);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddItem_WithInvalidQuantity_ShouldReturnBadRequest()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/cart/items",
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = -1
            });

        response.StatusCode.Should()
            .Be(HttpStatusCode.BadRequest);
    }

    // =========================================================
    // UPDATE ITEMS
    // =========================================================

    [Fact]
    public async Task UpdateItem_WithValidQuantity_ShouldPersistChanges()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        await AddItemAsync(client, product.Id, 2);

        var response = await client.PutAsJsonAsync(
            $"/cart/items/{product.Id}",
            new UpdateCartItemRequest
            {
                Quantity = 5
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await GetCartAsync(client);

        cart.Items.Single().Quantity.Should().Be(5);
        cart.TotalQuantity.Should().Be(5);
        cart.TotalPrice.Should().Be(1250m);
    }

    [Fact]
    public async Task UpdateItem_WhenProductNotInCart_ShouldReturnConflict()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/cart/items/{product.Id}",
            new UpdateCartItemRequest
            {
                Quantity = 3
            });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // =========================================================
    // REMOVE ITEMS
    // =========================================================

    [Fact]
    public async Task RemoveItem_WhenItemExists_ShouldRemoveItem()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        await AddItemAsync(client, product.Id, 2);

        var response = await client.DeleteAsync(
            $"/cart/items/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cart = await GetCartAsync(client);

        cart.Items.Should().BeEmpty();
        cart.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public async Task RemoveItem_WhenItemDoesNotExist_ShouldReturnNotFound()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var client = await CreateCustomerClientAsync(factory);

        var response = await client.DeleteAsync(
            "/cart/items/999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================================
    // CLEAR CART
    // =========================================================

    [Fact]
    public async Task ClearCart_ShouldRemoveItemsButPreserveCart()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var client = await CreateCustomerClientAsync(factory);

        var originalCart = await GetCartAsync(client);

        await AddItemAsync(client, product.Id, 3);

        var response = await client.DeleteAsync("/cart");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var clearedCart = await GetCartAsync(client);

        clearedCart.Id.Should().Be(originalCart.Id);
        clearedCart.Items.Should().BeEmpty();
        clearedCart.TotalQuantity.Should().Be(0);
        clearedCart.TotalPrice.Should().Be(0);
    }

    // =========================================================
    // USER ISOLATION
    // =========================================================

    [Fact]
    public async Task Cart_DifferentUsers_ShouldHaveIndependentCarts()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var firstClient = await CreateCustomerClientAsync(
            factory,
            "first@example.com");

        using var secondClient = await CreateCustomerClientAsync(
            factory,
            "second@example.com");

        await AddItemAsync(firstClient, product.Id, 3);

        var firstCart = await GetCartAsync(firstClient);
        var secondCart = await GetCartAsync(secondClient);

        firstCart.Id.Should().NotBe(secondCart.Id);

        firstCart.Items.Should().ContainSingle();
        firstCart.TotalQuantity.Should().Be(3);

        secondCart.Items.Should().BeEmpty();
        secondCart.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Cart_SecondUser_ShouldNotModifyFirstUsersCart()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory);

        using var firstClient = await CreateCustomerClientAsync(
            factory,
            "first@example.com");

        using var secondClient = await CreateCustomerClientAsync(
            factory,
            "second@example.com");

        await AddItemAsync(firstClient, product.Id, 3);

        var response = await secondClient.DeleteAsync(
            $"/cart/items/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var firstCart = await GetCartAsync(firstClient);

        firstCart.Items.Should().ContainSingle();
        firstCart.Items.Single().Quantity.Should().Be(3);
    }

    // =========================================================
    // STOCK MANAGEMENT
    // =========================================================

    [Fact]
    public async Task AddItem_ShouldNotReduceProductStock()
    {
        using var factory = new CardShopWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        var product = await CreateProductAsync(factory, stock: 10);

        using var client = await CreateCustomerClientAsync(factory);

        await AddItemAsync(client, product.Id, 4);

        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var savedProduct = await context.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);

        savedProduct.AvailableQuantity.Should().Be(10);
    }
}
