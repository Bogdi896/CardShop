
using CardShop.Application.DTOs.Carts;
using CardShop.Application.Validators.Carts;
using FluentAssertions;
using Xunit;

namespace CardShop.UnitTests.Validators;

public class CartValidatorTests
{
    private readonly AddCartItemRequestValidator _addValidator = new();
    private readonly UpdateCartItemRequestValidator _updateValidator = new();

    // =========================================================
    // ADD CART ITEM
    // =========================================================

    [Fact]
    public void Add_WithValidRequest_ShouldPass()
    {
        var request = new AddCartItemRequest
        {
            ProductId = 1,
            Quantity = 2
        };

        var result = _addValidator.Validate(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Add_WithInvalidProductId_ShouldFail(int productId)
    {
        var request = new AddCartItemRequest
        {
            ProductId = productId,
            Quantity = 1
        };

        var result = _addValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.ProductId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Add_WithInvalidQuantity_ShouldFail(int quantity)
    {
        var request = new AddCartItemRequest
        {
            ProductId = 1,
            Quantity = quantity
        };

        var result = _addValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Quantity));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Add_WithPositiveQuantity_ShouldPass(int quantity)
    {
        var request = new AddCartItemRequest
        {
            ProductId = 1,
            Quantity = quantity
        };

        var result = _addValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    // =========================================================
    // UPDATE CART ITEM
    // =========================================================

    [Fact]
    public void Update_WithValidRequest_ShouldPass()
    {
        var request = new UpdateCartItemRequest
        {
            Quantity = 5
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Update_WithInvalidQuantity_ShouldFail(int quantity)
    {
        var request = new UpdateCartItemRequest
        {
            Quantity = quantity
        };

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Quantity));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Update_WithPositiveQuantity_ShouldPass(int quantity)
    {
        var request = new UpdateCartItemRequest
        {
            Quantity = quantity
        };

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
