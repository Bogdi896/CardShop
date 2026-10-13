
using CardShop.Application.DTOs.Products;
using CardShop.Application.Validators.Product;
using CardShop.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CardShop.UnitTests.Validators;

public class ProductValidatorTests
{
    private readonly CreateProductRequestValidator _createValidator = new();
    private readonly UpdateProductRequestValidator _updateValidator = new();
    private readonly UpdateProductStockRequestValidator _stockValidator = new();
    private readonly SearchProductsQueryValidator _searchValidator = new();

    // =========================================================
    // TEST DATA
    // =========================================================

    private static CreateProductRequest ValidCreateRequest() => new()
    {
        Name = "Duel Masters Booster Box",
        Description = "Japanese booster box",
        Type = ProductType.BoosterBox,
        Price = 250m,
        AvailableQuantity = 10
    };

    private static UpdateProductRequest ValidUpdateRequest() => new()
    {
        Name = "Duel Masters Booster Box",
        Description = "Updated description",
        Type = ProductType.BoosterBox,
        Price = 200m
    };

    private static SearchProductsQuery ValidSearchQuery() => new()
    {
        Search = "Duel Masters",
        Type = ProductType.BoosterBox,
        MinPrice = 10m,
        MaxPrice = 500m,
        Page = 1,
        PageSize = 10
    };

    // =========================================================
    // CREATE PRODUCT VALIDATION TESTS
    // =========================================================

    [Fact]
    public void Create_WithValidRequest_ShouldPass()
    {
        var request = ValidCreateRequest();

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullName_ShouldFail(string? name)
    {
        var request = ValidCreateRequest();
        request.Name = name!;

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Name));
    }

    [Fact]
    public void Create_WithNameExceeding200Characters_ShouldFail()
    {
        var request = ValidCreateRequest();
        request.Name = new string('A', 201);

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Name));
    }

    [Fact]
    public void Create_WithNameOf200Characters_ShouldPass()
    {
        var request = ValidCreateRequest();
        request.Name = new string('A', 200);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNullDescription_ShouldFail()
    {
        var request = ValidCreateRequest();
        request.Description = null!;

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Description));
    }

    [Fact]
    public void Create_WithDescriptionExceeding2000Characters_ShouldFail()
    {
        var request = ValidCreateRequest();
        request.Description = new string('A', 2001);

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Description));
    }

    [Fact]
    public void Create_WithDescriptionOf2000Characters_ShouldPass()
    {
        var request = ValidCreateRequest();
        request.Description = new string('A', 2000);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyDescription_ShouldPass()
    {
        var request = ValidCreateRequest();
        request.Description = string.Empty;

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WithInvalidProductType_ShouldFail()
    {
        var request = ValidCreateRequest();
        request.Type = (ProductType)999;

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Type));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-100")]
    public void Create_WithNonPositivePrice_ShouldFail(string price)
    {
        var request = ValidCreateRequest();
        request.Price = decimal.Parse(
            price,
            System.Globalization.CultureInfo.InvariantCulture);

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Price));
    }

    [Fact]
    public void Create_WithPriceHavingMoreThanTwoDecimals_ShouldFail()
    {
        var request = ValidCreateRequest();
        request.Price = 10.999m;

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Price));
    }

    [Fact]
    public void Create_WithPriceHavingTwoDecimals_ShouldPass()
    {
        var request = ValidCreateRequest();
        request.Price = 10.99m;

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_WithPriceExceedingPrecision_ShouldFail()
    {
        var request = ValidCreateRequest();
        request.Price = 12345678901234567.89m;

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Price));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(int.MinValue)]
    public void Create_WithNegativeQuantity_ShouldFail(int quantity)
    {
        var request = ValidCreateRequest();
        request.AvailableQuantity = quantity;

        var result = _createValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName ==
                     nameof(request.AvailableQuantity));
    }

    [Fact]
    public void Create_WithZeroQuantity_ShouldPass()
    {
        var request = ValidCreateRequest();
        request.AvailableQuantity = 0;

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    // =========================================================
    // UPDATE PRODUCT VALIDATION TESTS
    // =========================================================

    [Fact]
    public void Update_WithValidRequest_ShouldPass()
    {
        var request = ValidUpdateRequest();

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Update_WithEmptyOrNullName_ShouldFail(string? name)
    {
        var request = ValidUpdateRequest();
        request.Name = name!;

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Name));
    }

    [Fact]
    public void Update_WithNameExceeding200Characters_ShouldFail()
    {
        var request = ValidUpdateRequest();
        request.Name = new string('A', 201);

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Name));
    }

    [Fact]
    public void Update_WithNameOf200Characters_ShouldPass()
    {
        var request = ValidUpdateRequest();
        request.Name = new string('A', 200);

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithNullDescription_ShouldFail()
    {
        var request = ValidUpdateRequest();
        request.Description = null!;

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Description));
    }

    [Fact]
    public void Update_WithDescriptionExceeding2000Characters_ShouldFail()
    {
        var request = ValidUpdateRequest();
        request.Description = new string('A', 2001);

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Description));
    }

    [Fact]
    public void Update_WithDescriptionOf2000Characters_ShouldPass()
    {
        var request = ValidUpdateRequest();
        request.Description = new string('A', 2000);

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithEmptyDescription_ShouldPass()
    {
        var request = ValidUpdateRequest();
        request.Description = string.Empty;

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithInvalidProductType_ShouldFail()
    {
        var request = ValidUpdateRequest();
        request.Type = (ProductType)999;

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Type));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-100")]
    public void Update_WithNonPositivePrice_ShouldFail(string price)
    {
        var request = ValidUpdateRequest();
        request.Price = decimal.Parse(
            price,
            System.Globalization.CultureInfo.InvariantCulture);

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Price));
    }

    [Fact]
    public void Update_WithPriceHavingMoreThanTwoDecimals_ShouldFail()
    {
        var request = ValidUpdateRequest();
        request.Price = 10.999m;

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Price));
    }

    [Fact]
    public void Update_WithPriceHavingTwoDecimals_ShouldPass()
    {
        var request = ValidUpdateRequest();
        request.Price = 10.99m;

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Update_WithPriceExceedingPrecision_ShouldFail()
    {
        var request = ValidUpdateRequest();
        request.Price = 12345678901234567.89m;

        var result = _updateValidator.Validate(request);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.Price));
    }

    // =========================================================
    // STOCK VALIDATION TESTS
    // =========================================================

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Stock_WithNonNegativeQuantity_ShouldPass(int quantity)
    {
        var request = new UpdateProductStockRequest
        {
            AvailableQuantity = quantity
        };

        var result = _stockValidator.Validate(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void Stock_WithNegativeQuantity_ShouldFail(int quantity)
    {
        var request = new UpdateProductStockRequest
        {
            AvailableQuantity = quantity
        };

        var result = _stockValidator.Validate(request);

        result.IsValid.Should().BeFalse();

        result.Errors.Should().Contain(
            error => error.PropertyName ==
                     nameof(request.AvailableQuantity));
    }

    // =========================================================
    // SEARCH VALIDATION TESTS
    // =========================================================

    [Fact]
    public void Search_WithValidQuery_ShouldPass()
    {
        var query = ValidSearchQuery();

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Search_WithDefaultQuery_ShouldPass()
    {
        var query = new SearchProductsQuery();

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Search_WithNullOptionalFilters_ShouldPass()
    {
        var query = ValidSearchQuery();

        query.Search = null;
        query.Type = null;
        query.MinPrice = null;
        query.MaxPrice = null;

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Search_WithSearchExceeding200Characters_ShouldFail()
    {
        var query = ValidSearchQuery();
        query.Search = new string('A', 201);

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.Search));
    }

    [Fact]
    public void Search_WithSearchOf200Characters_ShouldPass()
    {
        var query = ValidSearchQuery();
        query.Search = new string('A', 200);

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Search_WithInvalidProductType_ShouldFail()
    {
        var query = ValidSearchQuery();
        query.Type = (ProductType)999;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.Type));
    }

    [Fact]
    public void Search_WithNegativeMinPrice_ShouldFail()
    {
        var query = ValidSearchQuery();
        query.MinPrice = -1m;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.MinPrice));
    }

    [Fact]
    public void Search_WithNegativeMaxPrice_ShouldFail()
    {
        var query = ValidSearchQuery();
        query.MaxPrice = -1m;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.MaxPrice));
    }

    [Fact]
    public void Search_WithMinPriceGreaterThanMaxPrice_ShouldFail()
    {
        var query = ValidSearchQuery();
        query.MinPrice = 500m;
        query.MaxPrice = 100m;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.MaxPrice));
    }

    [Fact]
    public void Search_WithEqualMinAndMaxPrice_ShouldPass()
    {
        var query = ValidSearchQuery();
        query.MinPrice = 100m;
        query.MaxPrice = 100m;

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Search_WithZeroMinAndMaxPrice_ShouldPass()
    {
        var query = ValidSearchQuery();
        query.MinPrice = 0m;
        query.MaxPrice = 0m;

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Search_WithInvalidPage_ShouldFail(int page)
    {
        var query = ValidSearchQuery();
        query.Page = page;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Search_WithInvalidPageSize_ShouldFail(int pageSize)
    {
        var query = ValidSearchQuery();
        query.PageSize = pageSize;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Search_WithValidPageSize_ShouldPass(int pageSize)
    {
        var query = ValidSearchQuery();
        query.PageSize = pageSize;

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Search_WithPaginationOverflow_ShouldFail()
    {
        var query = ValidSearchQuery();
        query.Page = int.MaxValue;
        query.PageSize = 100;

        var result = _searchValidator.Validate(query);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(query.Page));
    }

    [Fact]
    public void Search_WithMaximumSafePage_ShouldPass()
    {
        var query = ValidSearchQuery();
        query.Page = 21474837;
        query.PageSize = 100;

        var result = _searchValidator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
