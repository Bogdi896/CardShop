
using System.Data;
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
using CardShop.Infrastructure.Identity;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Concurrency;

public class CartConcurrencyTests
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
    // HELPERS
    // =========================================================

    private static async Task<(HttpClient Client, string UserId)>
        CreateCustomerAsync(SqlServerWebApplicationFactory factory)
    {
        var client = factory.CreateClient();

        var email = $"cart-test-{Guid.NewGuid():N}@example.com";

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest
            {
                RegistrationId = Guid.NewGuid(),
                Email = email,
                Password = "CardShop123!",
                ConfirmPassword = "CardShop123!"
            });

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"registration returned: {body}");

        var auth = JsonSerializer.Deserialize<AuthResponse>(
            body,
            JsonOptions);

        auth.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        return (client, auth.UserId);
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

        using var loginResponse = await client.PostAsJsonAsync(
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

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient admin,
        int stock = 20)
    {
        using var response = await admin.PostAsJsonAsync(
            "/admin/products",
            new CreateProductRequest
            {
                Name = $"Cart Concurrency {Guid.NewGuid():N}",
                Description = "Cart locking test",
                Type = ProductType.BoosterBox,
                Price = 250m,
                AvailableQuantity = stock
            },
            JsonOptions);

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            $"product creation returned: {body}");

        var product = JsonSerializer.Deserialize<ProductResponse>(
            body,
            JsonOptions);

        product.Should().NotBeNull();

        return product!;
    }

    private static Task<HttpResponseMessage> AddItemAsync(
        HttpClient client,
        int productId,
        int quantity)
    {
        return client.PostAsJsonAsync(
            "/cart/items",
            new AddCartItemRequest
            {
                ProductId = productId,
                Quantity = quantity
            });
    }

    private static async Task<CartResponse> GetCartAsync(
        HttpClient client)
    {
        using var response = await client.GetAsync("/cart");

        response.EnsureSuccessStatusCode();

        var cart = await response.Content
            .ReadFromJsonAsync<CartResponse>(JsonOptions);

        return cart!;
    }

    private static async Task<int> GetCartIdAsync(
        SqlServerWebApplicationFactory factory,
        string userId)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        return await context.Carts
            .Where(cart => cart.UserId == userId)
            .Select(cart => cart.Id)
            .SingleAsync();
    }

    // =========================================================
    // TEST 1: TWO SIMULTANEOUS ADDITIONS
    // =========================================================

    [Fact]
    public async Task AddItem_TwoConcurrentRequests_ShouldAccumulateQuantity()
    {
        using var factory = new SqlServerWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminAsync(factory);
        var product = await CreateProductAsync(admin, stock: 20);

        var (client, _) = await CreateCustomerAsync(factory);
        using var customer = client;

        var firstTask = AddItemAsync(customer, product.Id, 2);
        var secondTask = AddItemAsync(customer, product.Id, 3);

        var responses = await Task.WhenAll(
            firstTask,
            secondTask);

        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        var secondBody = await secondResponse.Content.ReadAsStringAsync();

        firstResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"first request returned: {firstBody}");

        secondResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"second request returned: {secondBody}");

        var cart = await GetCartAsync(customer);

        cart.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(5);
        cart.TotalQuantity.Should().Be(5);
        cart.TotalPrice.Should().Be(1250m);
    }

    // =========================================================
    // TEST 2: CART MODIFICATION WAITS FOR LOCK
    // =========================================================

    [Fact]
    public async Task AddItem_WhenCartLocked_ShouldWaitUntilLockReleased()
    {
        using var factory = new SqlServerWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminAsync(factory);
        var product = await CreateProductAsync(admin);

        var (client, userId) = await CreateCustomerAsync(factory);
        using var customer = client;

        var cartId = await GetCartIdAsync(factory, userId);

        await using var scope = factory.Services.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // Hold an exclusive lock on this customer's cart.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE Carts
               SET UserId = UserId
               WHERE Id = {cartId}");

        // Start a cart modification while the lock is held.
        var addTask = AddItemAsync(
            customer,
            product.Id,
            2);

        try
        {
            // Give the request a short opportunity to execute.
            await Task.Delay(300);

            // It should not have completed while the lock is held.
            addTask.IsCompleted.Should().BeFalse(
                "the cart row is locked by another transaction");
        }
        finally
        {
            // Always release the lock, even if an assertion fails.
            await transaction.RollbackAsync();
        }

        using var response = await addTask;

        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"cart modification returned: {body}");

        var cart = await GetCartAsync(customer);

        cart.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(2);
    }

    // =========================================================
    // TEST 3: CHECKOUT WAITS FOR CART LOCK
    // =========================================================

    [Fact]
    public async Task Checkout_WhenCartLocked_ShouldWaitAndUseCommittedItems()
    {
        using var factory = new SqlServerWebApplicationFactory();
        await factory.InitializeDatabaseAsync();

        using var admin = await CreateAdminAsync(factory);
        var product = await CreateProductAsync(admin);

        var (client, userId) = await CreateCustomerAsync(factory);
        using var customer = client;

        // Start with two items.
        using (var response = await AddItemAsync(
            customer,
            product.Id,
            2))
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var cartId = await GetCartIdAsync(factory, userId);

        await using var scope = factory.Services.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // Hold the cart row lock.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE Carts
               SET UserId = UserId
               WHERE Id = {cartId}");

        var checkoutTask = customer.PostAsJsonAsync(
            "/orders/checkout",
            new CheckoutRequest
            {
                CheckoutId = Guid.NewGuid()
            });

        try
        {
            await Task.Delay(300);

            checkoutTask.IsCompleted.Should().BeFalse(
                "checkout should wait for the cart lock");
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        using var checkoutResponse = await checkoutTask;

        var body = await checkoutResponse.Content.ReadAsStringAsync();

        checkoutResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"checkout returned: {body}");

        var order = JsonSerializer.Deserialize<OrderResponse>(
            body,
            JsonOptions);

        order.Should().NotBeNull();
        order!.Items.Should().ContainSingle();
        order.Items.Single().Quantity.Should().Be(2);

        var cart = await GetCartAsync(customer);

        cart.Items.Should().BeEmpty();
    }
}
