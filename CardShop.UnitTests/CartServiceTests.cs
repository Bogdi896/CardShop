
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Carts;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Services;
using CardShop.Application.Validators.Carts;
using CardShop.Domain.Models;
using FluentAssertions;
using FluentValidation;
using Moq;
using Xunit;

namespace CardShop.UnitTests.Services;

public class CartServiceTests
{
    private const string UserId = "user-123";

    private readonly Mock<ICartRepository> _repositoryMock;
    private readonly Mock<ICartOperationExecutor> _executorMock;
    private readonly CartService _service;

    public CartServiceTests()
    {
        _repositoryMock = new Mock<ICartRepository>();
        _executorMock = new Mock<ICartOperationExecutor>();

        // Execute the callback using our mocked repository.
        // The real executor handles transactions and locking.
        _executorMock
            .Setup(executor => executor.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<CartResponse>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string userId,
                Func<ICartRepository, CancellationToken,
                    Task<CartResponse>> operation,
                CancellationToken token) =>
                    operation(_repositoryMock.Object, token));

        _executorMock
            .Setup(executor => executor.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<bool>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string userId,
                Func<ICartRepository, CancellationToken,
                    Task<bool>> operation,
                CancellationToken token) =>
                    operation(_repositoryMock.Object, token));

        _service = new CartService(
            _repositoryMock.Object,
            _executorMock.Object,
            new AddCartItemRequestValidator(),
            new UpdateCartItemRequestValidator());
    }

    private static Product CreateProduct(
        int id = 1,
        int stock = 10,
        bool isActive = true)
    {
        return new Product
        {
            Id = id,
            Name = "Duel Masters Booster Box",
            Description = "Japanese booster box",
            Price = 250m,
            AvailableQuantity = stock,
            IsActive = isActive
        };
    }

    private static Cart CreateCart(
        int quantity = 0,
        Product? product = null)
    {
        var cart = new Cart
        {
            Id = 1,
            UserId = UserId,
            Items = new List<CartItem>()
        };

        if (quantity > 0)
        {
            product ??= CreateProduct();

            cart.Items.Add(new CartItem
            {
                Id = 1,
                CartId = cart.Id,
                ProductId = product.Id,
                Product = product,
                Quantity = quantity
            });
        }

        return cart;
    }

    private void SetupCart(Cart cart)
    {
        _repositoryMock
            .Setup(repo => repo.GetByUserIdAsync(
                UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        _repositoryMock
            .Setup(repo => repo.GetForUpdateAsync(
                UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
    }

    private void SetupProduct(Product product)
    {
        _repositoryMock
            .Setup(repo => repo.GetProductByIdAsync(
                product.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
    }

    private void VerifySavedOnce()
    {
        _repositoryMock.Verify(
            repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    private void VerifyNeverSaved()
    {
        _repositoryMock.Verify(
            repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    // =========================================================
    // GET CART
    // =========================================================

    [Fact]
    public async Task GetCart_WithEmptyCart_ShouldReturnEmptyCart()
    {
        var cart = CreateCart();
        SetupCart(cart);

        var result = await _service.GetCartAsync(UserId);

        result.Id.Should().Be(cart.Id);
        result.Items.Should().BeEmpty();
        result.TotalPrice.Should().Be(0);
        result.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public async Task GetCart_WithItems_ShouldCalculateTotals()
    {
        var product = CreateProduct();
        var cart = CreateCart(3, product);
        SetupCart(cart);

        var result = await _service.GetCartAsync(UserId);

        result.Items.Should().ContainSingle();
        result.TotalQuantity.Should().Be(3);
        result.TotalPrice.Should().Be(750m);

        var item = result.Items.Single();

        item.ProductId.Should().Be(product.Id);
        item.ProductName.Should().Be(product.Name);
        item.UnitPrice.Should().Be(250m);
        item.TotalPrice.Should().Be(750m);
    }

    [Fact]
    public async Task GetCart_WhenCartDoesNotExist_ShouldThrowCartException()
    {
        _repositoryMock
            .Setup(repo => repo.GetByUserIdAsync(
                UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        Func<Task> act = () => _service.GetCartAsync(UserId);

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    // =========================================================
    // ADD ITEM
    // =========================================================

    [Fact]
    public async Task AddItem_WithValidProduct_ShouldAddItem()
    {
        var product = CreateProduct();
        var cart = CreateCart();

        SetupCart(cart);
        SetupProduct(product);

        var result = await _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 2
            });

        result.Items.Should().ContainSingle();
        result.TotalQuantity.Should().Be(2);
        result.TotalPrice.Should().Be(500m);

        cart.Items.Single().ProductId.Should().Be(product.Id);
        cart.Items.Single().Quantity.Should().Be(2);

        VerifySavedOnce();
    }

    [Fact]
    public async Task AddItem_WhenProductAlreadyExists_ShouldIncreaseQuantity()
    {
        var product = CreateProduct();
        var cart = CreateCart(3, product);

        SetupCart(cart);
        SetupProduct(product);

        var result = await _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 2
            });

        result.Items.Should().ContainSingle();
        result.TotalQuantity.Should().Be(5);
        cart.Items.Single().Quantity.Should().Be(5);

        VerifySavedOnce();
    }

    [Fact]
    public async Task AddItem_WhenQuantityEqualsAvailableStock_ShouldSucceed()
    {
        var product = CreateProduct(stock: 5);
        var cart = CreateCart();

        SetupCart(cart);
        SetupProduct(product);

        var result = await _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 5
            });

        result.TotalQuantity.Should().Be(5);

        VerifySavedOnce();
    }

    [Fact]
    public async Task AddItem_WhenQuantityExceedsStock_ShouldThrowCartException()
    {
        var product = CreateProduct(stock: 5);
        var cart = CreateCart();

        SetupCart(cart);
        SetupProduct(product);

        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 6
            });

        await act.Should().ThrowAsync<CartException>();

        cart.Items.Should().BeEmpty();
        VerifyNeverSaved();
    }

    [Fact]
    public async Task AddItem_WhenCombinedQuantityExceedsStock_ShouldThrow()
    {
        var product = CreateProduct(stock: 5);
        var cart = CreateCart(4, product);

        SetupCart(cart);
        SetupProduct(product);

        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 2
            });

        await act.Should().ThrowAsync<CartException>();

        cart.Items.Single().Quantity.Should().Be(4);
        VerifyNeverSaved();
    }

    [Fact]
    public async Task AddItem_WhenProductInactive_ShouldThrowCartException()
    {
        var product = CreateProduct(isActive: false);
        SetupCart(CreateCart());
        SetupProduct(product);

        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 1
            });

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    [Fact]
    public async Task AddItem_WhenProductDoesNotExist_ShouldThrowCartException()
    {
        SetupCart(CreateCart());

        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = 999,
                Quantity = 1
            });

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddItem_WithInvalidQuantity_ShouldThrowValidationException(
        int quantity)
    {
        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = 1,
                Quantity = quantity
            });

        await act.Should().ThrowAsync<ValidationException>();

        VerifyNeverSaved();
    }

    [Fact]
    public async Task AddItem_WithInvalidProductId_ShouldThrowValidationException()
    {
        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = 0,
                Quantity = 1
            });

        await act.Should().ThrowAsync<ValidationException>();

        VerifyNeverSaved();
    }

    [Fact]
    public async Task AddItem_WhenCartDoesNotExist_ShouldThrowCartException()
    {
        Func<Task> act = () => _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = 1,
                Quantity = 1
            });

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    // =========================================================
    // UPDATE ITEM
    // =========================================================

    [Fact]
    public async Task UpdateItem_WithValidQuantity_ShouldUpdateQuantity()
    {
        var product = CreateProduct();
        var cart = CreateCart(2, product);

        SetupCart(cart);
        SetupProduct(product);

        var result = await _service.UpdateItemAsync(
            UserId,
            product.Id,
            new UpdateCartItemRequest
            {
                Quantity = 5
            });

        result.TotalQuantity.Should().Be(5);
        result.TotalPrice.Should().Be(1250m);
        cart.Items.Single().Quantity.Should().Be(5);

        VerifySavedOnce();
    }

    [Fact]
    public async Task UpdateItem_WhenQuantityExceedsStock_ShouldThrow()
    {
        var product = CreateProduct(stock: 3);
        var cart = CreateCart(2, product);

        SetupCart(cart);
        SetupProduct(product);

        Func<Task> act = () => _service.UpdateItemAsync(
            UserId,
            product.Id,
            new UpdateCartItemRequest
            {
                Quantity = 4
            });

        await act.Should().ThrowAsync<CartException>();

        cart.Items.Single().Quantity.Should().Be(2);
        VerifyNeverSaved();
    }

    [Fact]
    public async Task UpdateItem_WhenProductNotInCart_ShouldThrow()
    {
        SetupCart(CreateCart());

        Func<Task> act = () => _service.UpdateItemAsync(
            UserId,
            1,
            new UpdateCartItemRequest
            {
                Quantity = 2
            });

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    [Fact]
    public async Task UpdateItem_WhenProductInactive_ShouldThrow()
    {
        var product = CreateProduct(isActive: false);
        SetupCart(CreateCart(2, product));
        SetupProduct(product);

        Func<Task> act = () => _service.UpdateItemAsync(
            UserId,
            product.Id,
            new UpdateCartItemRequest
            {
                Quantity = 3
            });

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateItem_WithInvalidQuantity_ShouldThrowValidationException(
        int quantity)
    {
        Func<Task> act = () => _service.UpdateItemAsync(
            UserId,
            1,
            new UpdateCartItemRequest
            {
                Quantity = quantity
            });

        await act.Should().ThrowAsync<ValidationException>();

        VerifyNeverSaved();
    }

    [Fact]
    public async Task UpdateItem_WhenCartDoesNotExist_ShouldThrow()
    {
        Func<Task> act = () => _service.UpdateItemAsync(
            UserId,
            1,
            new UpdateCartItemRequest
            {
                Quantity = 2
            });

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    // =========================================================
    // REMOVE ITEM
    // =========================================================

    [Fact]
    public async Task RemoveItem_WhenItemExists_ShouldRemoveAndSave()
    {
        var product = CreateProduct();
        var cart = CreateCart(2, product);
        SetupCart(cart);

        var item = cart.Items.Single();

        var result = await _service.RemoveItemAsync(
            UserId,
            product.Id);

        result.Should().BeTrue();

        _repositoryMock.Verify(
            repo => repo.RemoveItem(item),
            Times.Once());

        VerifySavedOnce();
    }

    [Fact]
    public async Task RemoveItem_WhenItemDoesNotExist_ShouldReturnFalse()
    {
        SetupCart(CreateCart());

        var result = await _service.RemoveItemAsync(
            UserId,
            999);

        result.Should().BeFalse();

        _repositoryMock.Verify(
            repo => repo.RemoveItem(It.IsAny<CartItem>()),
            Times.Never());

        VerifyNeverSaved();
    }

    [Fact]
    public async Task RemoveItem_WhenCartDoesNotExist_ShouldThrow()
    {
        Func<Task> act = () => _service.RemoveItemAsync(
            UserId,
            1);

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    // =========================================================
    // CLEAR CART
    // =========================================================

    [Fact]
    public async Task ClearCart_WithItems_ShouldClearAndSave()
    {
        var cart = CreateCart(3);
        SetupCart(cart);

        await _service.ClearCartAsync(UserId);

        _repositoryMock.Verify(
            repo => repo.ClearItems(cart),
            Times.Once());

        VerifySavedOnce();
    }

    [Fact]
    public async Task ClearCart_WithEmptyCart_ShouldStillSucceed()
    {
        var cart = CreateCart();
        SetupCart(cart);

        await _service.ClearCartAsync(UserId);

        _repositoryMock.Verify(
            repo => repo.ClearItems(cart),
            Times.Once());

        VerifySavedOnce();
    }

    [Fact]
    public async Task ClearCart_WhenCartDoesNotExist_ShouldThrow()
    {
        Func<Task> act = () => _service.ClearCartAsync(UserId);

        await act.Should().ThrowAsync<CartException>();

        VerifyNeverSaved();
    }

    // =========================================================
    // EXECUTOR BEHAVIOR
    // =========================================================

    [Fact]
    public async Task AddItem_ShouldUseExecutorWithCorrectUserId()
    {
        var product = CreateProduct();
        SetupCart(CreateCart());
        SetupProduct(product);

        await _service.AddItemAsync(
            UserId,
            new AddCartItemRequest
            {
                ProductId = product.Id,
                Quantity = 1
            });

        _executorMock.Verify(
            executor => executor.ExecuteAsync(
                UserId,
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<CartResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task UpdateItem_ShouldUseExecutorWithCorrectUserId()
    {
        var product = CreateProduct();
        SetupCart(CreateCart(2, product));
        SetupProduct(product);

        await _service.UpdateItemAsync(
            UserId,
            product.Id,
            new UpdateCartItemRequest
            {
                Quantity = 3
            });

        _executorMock.Verify(
            executor => executor.ExecuteAsync(
                UserId,
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<CartResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task RemoveItem_ShouldUseExecutorWithCorrectUserId()
    {
        var product = CreateProduct();
        SetupCart(CreateCart(2, product));

        await _service.RemoveItemAsync(
            UserId,
            product.Id);

        _executorMock.Verify(
            executor => executor.ExecuteAsync(
                UserId,
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<bool>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task ClearCart_ShouldUseExecutorWithCorrectUserId()
    {
        SetupCart(CreateCart(2));

        await _service.ClearCartAsync(UserId);

        _executorMock.Verify(
            executor => executor.ExecuteAsync(
                UserId,
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<bool>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task GetCart_ShouldNotUseExecutor()
    {
        SetupCart(CreateCart());

        await _service.GetCartAsync(UserId);

        _executorMock.Verify(
            executor => executor.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<CartResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never());

        _executorMock.Verify(
            executor => executor.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<Func<
                    ICartRepository,
                    CancellationToken,
                    Task<bool>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
