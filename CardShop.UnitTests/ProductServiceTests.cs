using CardShop.Application.DTOs.Products;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Services;
using CardShop.Application.Validators;
using CardShop.Application.Validators.Products;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;
using FluentAssertions;
using FluentValidation;     
using Moq;
using Xunit;

namespace CardShop.UnitTests.Services;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repositoryMock;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _repositoryMock = new Mock<IProductRepository>();

        _service = new ProductService(
            _repositoryMock.Object,
            new CreateProductRequestValidator(),
            new UpdateProductRequestValidator(),
            new UpdateProductStockRequestValidator(),
            new SearchProductsQueryValidator());
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateProduct()
    {
        var request = new CreateProductRequest
        {
            Name = "  Duel Masters Booster Box  ",
            Description = "  Japanese booster box  ",
            Type = ProductType.BoosterBox,
            Price = 250m,
            AvailableQuantity = 10
        };

        _repositoryMock
            .Setup(repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("Duel Masters Booster Box");
        result.Description.Should().Be("Japanese booster box");
        result.Price.Should().Be(250m);
        result.AvailableQuantity.Should().Be(10);
        result.IsActive.Should().BeTrue();

        _repositoryMock.Verify(
            repository => repository.Add(It.Is<Product>(
                product => product.Name == "Duel Masters Booster Box" &&
                           product.Description == "Japanese booster box" &&
                           product.Price == request.Price &&
                           product.IsActive)),
            Times.Once());

        _repositoryMock.Verify(
            repository => repository.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ShouldThrowValidationException_AndNotCallRepository()
    {
        var request = new CreateProductRequest
        {
            Name = string.Empty,
            Description = "Valid description",
            Type = ProductType.BoosterBox,
            Price = 0,
            AvailableQuantity = -1
        };

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationException>();

        _repositoryMock.Verify(repository => repository.Add(It.IsAny<Product>()), Times.Never);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductDoesNotExist_ShouldReturnNull()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _service.GetByIdAsync(1);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductIsInactiveAndIncludeInactiveIsFalse_ShouldReturnNull()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Product",
                Description = "Description",
                Type = ProductType.Card,
                Price = 10,
                AvailableQuantity = 3,
                IsActive = false
            });

        var result = await _service.GetByIdAsync(1, includeInactive: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductIsInactiveAndIncludeInactiveIsTrue_ShouldReturnProduct()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product
            {
                Id = 1,
                Name = "Product",
                Description = "Description",
                Type = ProductType.Card,
                Price = 10,
                AvailableQuantity = 3,
                IsActive = false
            });

        var result = await _service.GetByIdAsync(1, includeInactive: true);

        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_WithInvalidQuery_ShouldThrowValidationException()
    {
        var query = new SearchProductsQuery
        {
            Page = 0,
            PageSize = 101
        };

        var act = () => _service.SearchAsync(query);

        await act.Should().ThrowAsync<ValidationException>();

        _repositoryMock.Verify(repository => repository.SearchAsync(
            It.IsAny<SearchProductsQuery>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithValidQuery_ShouldReturnMappedPagedResult()
    {
        var query = new SearchProductsQuery
        {
            Search = "booster",
            Page = 2,
            PageSize = 1
        };

        var pagedProducts = new Application.Common.PagedResult<Product>
        {
            Items = new List<Product>
            {
                new()
                {
                    Id = 5,
                    Name = "Booster Box",
                    Description = "Set",
                    Type = ProductType.BoosterBox,
                    Price = 200,
                    AvailableQuantity = 4,
                    IsActive = true
                }
            },
            TotalCount = 3,
            Page = 2,
            PageSize = 1
        };

        _repositoryMock
            .Setup(repository => repository.SearchAsync(query, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedProducts);

        var result = await _service.SearchAsync(query, includeInactive: true);

        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(5);
        result.Items[0].Name.Should().Be("Booster Box");
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidRequest_ShouldThrowValidationException()
    {
        var request = new UpdateProductRequest
        {
            Name = string.Empty,
            Description = "Valid",
            Type = ProductType.BoosterBox,
            Price = 0
        };

        var act = () => _service.UpdateAsync(1, request);

        await act.Should().ThrowAsync<ValidationException>();

        _repositoryMock.Verify(repository => repository.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductDoesNotExist_ShouldReturnNull()
    {
        var request = new UpdateProductRequest
        {
            Name = "Updated",
            Description = "Updated description",
            Type = ProductType.BoosterBox,
            Price = 100
        };

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _service.UpdateAsync(1, request);

        result.Should().BeNull();
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenProductExists_ShouldUpdateAndReturnMappedDto()
    {
        var request = new UpdateProductRequest
        {
            Name = "  Updated Product  ",
            Description = "  Updated description  ",
            Type = ProductType.BoosterPack,
            Price = 42
        };

        var existingProduct = new Product
        {
            Id = 7,
            Name = "Old name",
            Description = "Old description",
            Type = ProductType.Card,
            Price = 12,
            AvailableQuantity = 10,
            IsActive = true
        };

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProduct);

        var result = await _service.UpdateAsync(7, request);

        result.Should().NotBeNull();
        result!.Id.Should().Be(7);
        result.Name.Should().Be("Updated Product");
        result.Description.Should().Be("Updated description");
        result.Type.Should().Be(ProductType.BoosterPack);
        result.Price.Should().Be(42);

        existingProduct.Name.Should().Be("Updated Product");
        existingProduct.Description.Should().Be("Updated description");
        existingProduct.Type.Should().Be(ProductType.BoosterPack);
        existingProduct.Price.Should().Be(42);

        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStockAsync_WithInvalidRequest_ShouldThrowValidationException()
    {
        var request = new UpdateProductStockRequest
        {
            AvailableQuantity = -1
        };

        var act = () => _service.UpdateStockAsync(1, request);

        await act.Should().ThrowAsync<ValidationException>();

        _repositoryMock.Verify(repository => repository.GetForUpdateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStockAsync_WhenProductDoesNotExist_ShouldReturnNull()
    {
        var request = new UpdateProductStockRequest
        {
            AvailableQuantity = 15
        };

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _service.UpdateStockAsync(2, request);

        result.Should().BeNull();
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStockAsync_WhenProductExists_ShouldUpdateQuantityAndReturnDto()
    {
        var request = new UpdateProductStockRequest
        {
            AvailableQuantity = 99
        };

        var existingProduct = new Product
        {
            Id = 2,
            Name = "Product",
            Description = "Description",
            Type = ProductType.Card,
            Price = 12,
            AvailableQuantity = 1,
            IsActive = true
        };

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProduct);

        var result = await _service.UpdateStockAsync(2, request);

        result.Should().NotBeNull();
        result!.AvailableQuantity.Should().Be(99);
        existingProduct.AvailableQuantity.Should().Be(99);

        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_WhenProductDoesNotExist_ShouldReturnFalse()
    {
        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _service.SetActiveAsync(3, true);

        result.Should().BeFalse();
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetActiveAsync_WhenProductExists_ShouldUpdateFlagAndReturnTrue(bool isActive)
    {
        var product = new Product
        {
            Id = 4,
            Name = "Product",
            Description = "Description",
            Type = ProductType.BoosterPack,
            Price = 6,
            AvailableQuantity = 20,
            IsActive = !isActive
        };

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _service.SetActiveAsync(4, isActive);

        result.Should().BeTrue();
        product.IsActive.Should().Be(isActive);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldPassCancellationTokenToRepository()
    {
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(repository => repository.GetByIdAsync(1, cancellationToken))
            .ReturnsAsync((Product?)null);

        await _service.GetByIdAsync(1, cancellationToken: cancellationToken);

        _repositoryMock.Verify(repository => repository.GetByIdAsync(1, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ShouldPassCancellationTokenToRepository()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var query = new SearchProductsQuery { Page = 1, PageSize = 10 };

        _repositoryMock
            .Setup(repository => repository.SearchAsync(query, false, cancellationToken))
            .ReturnsAsync(new Application.Common.PagedResult<Product>());

        await _service.SearchAsync(query, cancellationToken: cancellationToken);

        _repositoryMock.Verify(repository => repository.SearchAsync(query, false, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPassCancellationTokenToRepositoryMethods()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var request = new UpdateProductRequest
        {
            Name = "Updated",
            Description = "Updated",
            Type = ProductType.BoosterBox,
            Price = 10
        };

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(8, cancellationToken))
            .ReturnsAsync(new Product
            {
                Id = 8,
                Name = "Name",
                Description = "Description",
                Type = ProductType.Card,
                Price = 2,
                AvailableQuantity = 1,
                IsActive = true
            });

        _repositoryMock
            .Setup(repository => repository.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await _service.UpdateAsync(8, request, cancellationToken);

        _repositoryMock.Verify(repository => repository.GetForUpdateAsync(8, cancellationToken), Times.Once);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task UpdateStockAsync_ShouldPassCancellationTokenToRepositoryMethods()
    {
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(2, cancellationToken))
            .ReturnsAsync(new Product
            {
                Id = 2,
                Name = "Product",
                Description = "Description",
                Type = ProductType.Card,
                Price = 12,
                AvailableQuantity = 1,
                IsActive = true
            });

        _repositoryMock
            .Setup(repository => repository.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await _service.UpdateStockAsync(2, new UpdateProductStockRequest { AvailableQuantity = 2 }, cancellationToken);

        _repositoryMock.Verify(repository => repository.GetForUpdateAsync(2, cancellationToken), Times.Once);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_ShouldPassCancellationTokenToRepositoryMethods()
    {
        var cancellationToken = new CancellationTokenSource().Token;

        _repositoryMock
            .Setup(repository => repository.GetForUpdateAsync(4, cancellationToken))
            .ReturnsAsync(new Product
            {
                Id = 4,
                Name = "Product",
                Description = "Description",
                Type = ProductType.BoosterPack,
                Price = 6,
                AvailableQuantity = 20,
                IsActive = false
            });

        _repositoryMock
            .Setup(repository => repository.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await _service.SetActiveAsync(4, true, cancellationToken);

        _repositoryMock.Verify(repository => repository.GetForUpdateAsync(4, cancellationToken), Times.Once);
        _repositoryMock.Verify(repository => repository.SaveChangesAsync(cancellationToken), Times.Once);
    }
}