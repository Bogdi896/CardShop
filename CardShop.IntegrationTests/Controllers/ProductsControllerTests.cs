using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardShop.Application.DTOs.Products;
using CardShop.Domain.Enums;
using CardShop.IntegrationTests.Helpers;
using CardShop.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CardShop.IntegrationTests.Controllers;

public class ProductsControllerTests
{
    [Fact]
    public async Task CreateProduct_WithValidRequest_ShouldReturnCreated()
    {
        // Arrange
        using var factory = new CardShopWebApplicationFactory();

        await factory.InitializeDatabaseAsync();

        using var client = factory.CreateClient();

        await AuthenticationHelper.AuthenticateAsAdminAsync(
            client,
            factory);

        var request = new CreateProductRequest
        {
            Name = "Duel Masters Booster Box",
            Description = "Japanese booster box",
            Type = ProductType.BoosterBox,
            Price = 250m,
            AvailableQuantity = 10
        };

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));

        // Act
        var response = await client.PostAsJsonAsync(
            "/admin/products",
            request,
            jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var product = await response.Content
            .ReadFromJsonAsync<ProductResponse>(jsonOptions);

        product.Should().NotBeNull();
        product!.Id.Should().BeGreaterThan(0);
        product.Name.Should().Be(request.Name);
        product.Price.Should().Be(request.Price);
        product.AvailableQuantity.Should().Be(10);
        product.IsActive.Should().BeTrue();

        // Verify the product is accessible through the public endpoint.
        var getResponse = await client.GetAsync(
            $"/products/{product.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var savedProduct = await getResponse.Content
            .ReadFromJsonAsync<ProductResponse>(jsonOptions);

        savedProduct.Should().NotBeNull();
        savedProduct!.Id.Should().Be(product.Id);
        savedProduct.Name.Should().Be(request.Name);
    }
}
