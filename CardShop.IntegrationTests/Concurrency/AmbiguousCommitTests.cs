
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.DTOs.Orders;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;
using CardShop.Infrastructure.Persistence;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardShop.IntegrationTests.Concurrency;

public class AmbiguousCommitTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }

    [Fact]
    public async Task Checkout_WhenCommitConfirmationIsLost_ShouldRecoverOrder()
    {
        // =====================================================
        // ARRANGE: INITIALIZE SQL SERVER
        // =====================================================

        using var factory = new CommitFailureWebApplicationFactory();

        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        // =====================================================
        // ARRANGE: REGISTER CUSTOMER
        // =====================================================

        var email =
            $"commit-test-{Guid.NewGuid():N}@example.com";

        using var registrationResponse =
            await client.PostAsJsonAsync(
                "/auth/register",
                new RegisterRequest
                {
                    Email = email,
                    Password = "CardShop123!",
                    ConfirmPassword = "CardShop123!"
                });

        var registrationBody =
            await registrationResponse.Content.ReadAsStringAsync();

        registrationResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"registration returned: {registrationBody}");

        var auth = JsonSerializer.Deserialize<AuthResponse>(
            registrationBody,
            JsonOptions);

        auth.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                auth!.AccessToken);

        // =====================================================
        // ARRANGE: CREATE PRODUCT
        // =====================================================

        int productId;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            var product = new Product
            {
                Name = $"Commit Test {Guid.NewGuid():N}",
                Description = "Ambiguous commit test product",
                Type = ProductType.BoosterBox,
                Price = 250m,
                AvailableQuantity = 10,
                IsActive = true
            };

            context.Products.Add(product);

            await context.SaveChangesAsync();

            productId = product.Id;
        }

        // =====================================================
        // ARRANGE: ADD PRODUCT TO CART
        // =====================================================

        using var addResponse = await client.PostAsJsonAsync(
            "/cart/items",
            new AddCartItemRequest
            {
                ProductId = productId,
                Quantity = 2
            });

        var addBody =
            await addResponse.Content.ReadAsStringAsync();

        addResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"adding to cart returned: {addBody}");

        var checkoutId = Guid.NewGuid();

        // =====================================================
        // ACT: SIMULATE LOST COMMIT CONFIRMATION
        // =====================================================

        // Everything is prepared. The NEXT successful
        // transaction commit will trigger our exception.
        factory.CommitInterceptor.Arm();

        using var checkoutResponse =
            await client.PostAsJsonAsync(
                "/orders/checkout",
                new CheckoutRequest
                {
                    CheckoutId = checkoutId
                });

        var checkoutBody =
            await checkoutResponse.Content.ReadAsStringAsync();

        // =====================================================
        // ASSERT: HTTP RESPONSE
        // =====================================================

        checkoutResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"checkout returned: {checkoutBody}");

        var order = JsonSerializer.Deserialize<OrderResponse>(
            checkoutBody,
            JsonOptions);

        order.Should().NotBeNull();

        order!.CheckoutId.Should().Be(checkoutId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(500m);

        order.Items.Should().ContainSingle();
        order.Items.Single().Quantity.Should().Be(2);

        // =====================================================
        // ASSERT: FAILURE WAS ACTUALLY INJECTED
        // =====================================================

        factory.CommitInterceptor.FailureCount
            .Should().Be(
                1,
                "the test must actually simulate a lost " +
                "commit confirmation");

        // =====================================================
        // ASSERT: DATABASE STATE
        // =====================================================

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            var savedOrders = await context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Where(o =>
                    o.UserId == auth.UserId &&
                    o.CheckoutId == checkoutId)
                .ToListAsync();

            savedOrders.Should().ContainSingle();

            var savedOrder = savedOrders.Single();

            savedOrder.Id.Should().Be(order.Id);
            savedOrder.TotalAmount.Should().Be(500m);
            savedOrder.Status.Should().Be(OrderStatus.Pending);

            savedOrder.Items.Should().ContainSingle();

            var remainingStock = await context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => p.AvailableQuantity)
                .SingleAsync();

            remainingStock.Should().Be(
                8,
                "stock must be deducted exactly once");

            var cart = await context.Carts
                .AsNoTracking()
                .Include(c => c.Items)
                .SingleAsync(c => c.UserId == auth.UserId);

            cart.Items.Should().BeEmpty();
        }

        // =====================================================
        // ASSERT: RETRYING SAME CHECKOUT ID
        // =====================================================

        using var retryResponse =
            await client.PostAsJsonAsync(
                "/orders/checkout",
                new CheckoutRequest
                {
                    CheckoutId = checkoutId
                });

        retryResponse.StatusCode.Should().Be(
            HttpStatusCode.OK);

        var retryOrder = await retryResponse.Content
            .ReadFromJsonAsync<OrderResponse>(JsonOptions);

        retryOrder.Should().NotBeNull();

        retryOrder!.Id.Should().Be(order.Id);

        factory.CommitInterceptor.FailureCount
            .Should().Be(1);
    }
}
