
using CardShop.Application.Common;
using CardShop.Application.Common.Exceptions;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Services;
using CardShop.Application.Validators.Orders;
using CardShop.Domain.Enums;
using CardShop.Domain.Models;
using FluentAssertions;
using FluentValidation;
using Moq;
using Xunit;

namespace CardShop.UnitTests.Services;

public class OrderServiceTests
{
    private const string UserId = "user-123";
    private const string OtherUserId = "user-456";

    private readonly Mock<IOrderRepository> _repositoryMock;
    private readonly Mock<IOrderTransaction> _transactionMock;
    private readonly Mock<ICheckoutExecutor> _executorMock;

    private readonly OrderService _service;


    public OrderServiceTests()
    {
        _repositoryMock = new Mock<IOrderRepository>();
        _transactionMock = new Mock<IOrderTransaction>();
        _executorMock = new Mock<ICheckoutExecutor>();

        _repositoryMock
            .Setup(repo => repo.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        // =========================================================
        // CHECKOUT EXECUTOR
        // =========================================================

        // Checkout now uses ExecuteCheckoutAsync instead of
        // the generic ExecuteAsync method.
        _executorMock
            .Setup(executor => executor.ExecuteCheckoutAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Func<
                    IOrderRepository,
                    CancellationToken,
                    Task<OrderResponse>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string userId,
                Guid checkoutId,
                Func<IOrderRepository, CancellationToken,
                    Task<OrderResponse>> operation,
                CancellationToken token) =>
                    operation(_repositoryMock.Object, token));

        // =========================================================
        // GENERAL EXECUTOR
        // Used by shipping and cancellation.
        // =========================================================

        _executorMock
            .Setup(executor => executor.ExecuteAsync(
                It.IsAny<Func<
                    IOrderRepository,
                    CancellationToken,
                    Task<bool>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                Func<IOrderRepository, CancellationToken,
                    Task<bool>> operation,
                CancellationToken token) =>
                    operation(_repositoryMock.Object, token));

        _service = new OrderService(
            _repositoryMock.Object,
            _executorMock.Object,
            new CheckoutRequestValidator(),
            new SearchOrdersQueryValidator());
    }


    private static Product CreateProduct(
        int id = 1,
        int stock = 10,
        bool isActive = true,
        decimal price = 250m)
    {
        return new Product
        {
            Id = id,
            Name = $"Duel Masters Product {id}",
            Description = "Test product",
            Price = price,
            AvailableQuantity = stock,
            IsActive = isActive
        };
    }

    private static Cart CreateCart(
        Product? product = null,
        int quantity = 2)
    {
        var cart = new Cart
        {
            Id = 1,
            UserId = UserId,
            Items = new List<CartItem>()
        };

        if (product is not null)
        {
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

    private static Order CreateOrder(
        int id = 1,
        string userId = UserId,
        OrderStatus status = OrderStatus.Pending)
    {
        var product = CreateProduct();

        return new Order
        {
            Id = id,
            UserId = userId,
            CheckoutId = Guid.NewGuid(),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            TotalAmount = 500m,
            Items = new List<OrderItem>
            {
                new()
                {
                    Id = 1,
                    OrderId = id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = 2,
                    UnitPrice = 250m
                }
            }
        };
    }

    private static CheckoutRequest ValidCheckoutRequest() => new()
    {
        CheckoutId = Guid.NewGuid()
    };

    private void SetupCart(Cart cart)
    {
        _repositoryMock
            .Setup(r => r.GetCartAsync(
                UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
    }

    private void SetupStockDeduction(bool succeeds = true)
    {
        _repositoryMock
            .Setup(r => r.DeductStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds);
    }

    private void VerifyCommittedOnce()
    {
        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }

    private void VerifyNeverCommitted()
    {
        _transactionMock.Verify(
            t => t.CommitAsync(It.IsAny<CancellationToken>()),
            Times.Never());
    }

    // =========================================================
    // CHECKOUT
    // =========================================================

    [Fact]
    public async Task Checkout_WithValidCart_ShouldCreateOrder()
    {
        var product = CreateProduct();
        var cart = CreateCart(product);
        var request = ValidCheckoutRequest();

        SetupCart(cart);
        SetupStockDeduction();

        var result = await _service.CheckoutAsync(
            UserId,
            request);

        result.UserId.Should().Be(UserId);
        result.CheckoutId.Should().Be(request.CheckoutId);
        result.Status.Should().Be(OrderStatus.Pending);
        result.TotalAmount.Should().Be(500m);

        result.Items.Should().ContainSingle();
        result.Items.Single().Quantity.Should().Be(2);
        result.Items.Single().UnitPrice.Should().Be(250m);

        _repositoryMock.Verify(
            repo => repo.DeductStockAsync(
                product.Id,
                2,
                It.IsAny<CancellationToken>()),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.Add(It.Is<Order>(
                order =>
                    order.UserId == UserId &&
                    order.CheckoutId == request.CheckoutId &&
                    order.Status == OrderStatus.Pending &&
                    order.TotalAmount == 500m)),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.ClearCart(cart),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once());

        _executorMock.Verify(
            executor => executor.ExecuteCheckoutAsync(
                UserId,
                request.CheckoutId,
                It.IsAny<Func<
                    IOrderRepository,
                    CancellationToken,
                    Task<OrderResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_ShouldThrow()
    {
        SetupCart(CreateCart());

        Func<Task> act = () =>
            _service.CheckoutAsync(UserId, ValidCheckoutRequest());

        await act.Should().ThrowAsync<OrderException>()
            .WithMessage("*empty cart*");

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Checkout_WhenCartMissing_ShouldThrow()
    {
        Func<Task> act = () =>
            _service.CheckoutAsync(UserId, ValidCheckoutRequest());

        await act.Should().ThrowAsync<OrderException>();

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Checkout_WithInactiveProduct_ShouldThrow()
    {
        SetupCart(CreateCart(CreateProduct(isActive: false)));

        Func<Task> act = () =>
            _service.CheckoutAsync(UserId, ValidCheckoutRequest());

        await act.Should().ThrowAsync<OrderException>();

        _repositoryMock.Verify(
            r => r.DeductStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never());

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Checkout_WithInsufficientStock_ShouldThrow()
    {
        SetupCart(CreateCart(CreateProduct(stock: 1), 2));
        SetupStockDeduction(false);

        Func<Task> act = () =>
            _service.CheckoutAsync(UserId, ValidCheckoutRequest());

        await act.Should().ThrowAsync<OrderException>()
            .WithMessage("*Insufficient stock*");

        _repositoryMock.Verify(
            r => r.ClearCart(It.IsAny<Cart>()),
            Times.Never());

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Checkout_WithInvalidQuantity_ShouldThrow()
    {
        SetupCart(CreateCart(CreateProduct(), 0));

        Func<Task> act = () =>
            _service.CheckoutAsync(UserId, ValidCheckoutRequest());

        await act.Should().ThrowAsync<OrderException>();

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Checkout_WithEmptyGuid_ShouldFailValidation()
    {
        Func<Task> act = () => _service.CheckoutAsync(
            UserId,
            new CheckoutRequest { CheckoutId = Guid.Empty });

        await act.Should().ThrowAsync<ValidationException>();

        _executorMock.Verify(
            e => e.ExecuteAsync(
                It.IsAny<Func<
                    IOrderRepository,
                    CancellationToken,
                    Task<OrderResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Checkout_WithExistingId_ShouldReturnExistingOrder()
    {
        var order = CreateOrder();
        var request = new CheckoutRequest
        {
            CheckoutId = order.CheckoutId
        };

        _repositoryMock
            .Setup(r => r.GetByCheckoutIdAsync(
                UserId,
                request.CheckoutId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.CheckoutAsync(UserId, request);

        result.Id.Should().Be(order.Id);

        _repositoryMock.Verify(
            r => r.BeginTransactionAsync(
                It.IsAny<CancellationToken>()),
            Times.Never());

        _repositoryMock.Verify(
            r => r.DeductStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Checkout_WhenOrderAppearsAfterLock_ShouldReturnExisting()
    {
        var order = CreateOrder();

        var request = new CheckoutRequest
        {
            CheckoutId = order.CheckoutId
        };

        _repositoryMock
            .Setup(repo => repo.GetByCheckoutIdAsync(
                UserId,
                request.CheckoutId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.CheckoutAsync(
            UserId,
            request);

        result.Id.Should().Be(order.Id);

        _repositoryMock.Verify(
            repo => repo.LockCartAsync(
                UserId,
                It.IsAny<CancellationToken>()),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.DeductStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Checkout_WithMultipleProducts_ShouldCalculateTotal()
    {
        var first = CreateProduct(id: 1, price: 250m);
        var second = CreateProduct(id: 2, price: 80m);

        var cart = CreateCart(first, 2);

        cart.Items.Add(new CartItem
        {
            Id = 2,
            CartId = cart.Id,
            ProductId = second.Id,
            Product = second,
            Quantity = 3
        });

        SetupCart(cart);
        SetupStockDeduction();

        var request = ValidCheckoutRequest();

        var result = await _service.CheckoutAsync(
            UserId,
            request);

        result.Items.Should().HaveCount(2);
        result.TotalAmount.Should().Be(740m);

        _repositoryMock.Verify(
            repo => repo.DeductStockAsync(
                first.Id,
                2,
                It.IsAny<CancellationToken>()),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.DeductStockAsync(
                second.Id,
                3,
                It.IsAny<CancellationToken>()),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.Add(It.Is<Order>(
                order => order.TotalAmount == 740m)),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.ClearCart(cart),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once());

        _executorMock.Verify(
            executor => executor.ExecuteCheckoutAsync(
                UserId,
                request.CheckoutId,
                It.IsAny<Func<
                    IOrderRepository,
                    CancellationToken,
                    Task<OrderResponse>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Checkout_WhenSaveFails_ShouldPropagateException()
    {
        SetupCart(CreateCart(CreateProduct()));
        SetupStockDeduction();

        _repositoryMock
            .Setup(repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Database save failed."));

        Func<Task> act = () => _service.CheckoutAsync(
            UserId,
            ValidCheckoutRequest());

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Database save failed.");

        _repositoryMock.Verify(
            repo => repo.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once());

        _repositoryMock.Verify(
            repo => repo.ClearCart(It.IsAny<Cart>()),
            Times.Once());
    }

    // =========================================================
    // GET ORDER
    // =========================================================

    [Fact]
    public async Task GetById_WhenOrderExists_ShouldReturnOrder()
    {
        var order = CreateOrder();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.GetByIdAsync(
            order.Id,
            UserId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetById_WhenMissing_ShouldReturnNull()
    {
        var result = await _service.GetByIdAsync(999, UserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WithAnotherUser_ShouldReturnNull()
    {
        var order = CreateOrder(userId: OtherUserId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.GetByIdAsync(
            order.Id,
            UserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_AsAdmin_ShouldReturnAnyOrder()
    {
        var order = CreateOrder(userId: OtherUserId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await _service.GetByIdAsync(
            order.Id,
            null);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(OtherUserId);
    }

    // =========================================================
    // SEARCH
    // =========================================================

    [Fact]
    public async Task Search_WithValidQuery_ShouldReturnResults()
    {
        var query = new SearchOrdersQuery
        {
            Page = 1,
            PageSize = 10
        };

        var orders = new List<Order>
        {
            CreateOrder(id: 1),
            CreateOrder(id: 2)
        };

        _repositoryMock
            .Setup(r => r.SearchAsync(
                query,
                UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Order>
            {
                Items = orders,
                TotalCount = 2,
                Page = 1,
                PageSize = 10
            });

        var result = await _service.SearchAsync(query, UserId);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Search_WithInvalidPagination_ShouldThrow(
        int page,
        int pageSize)
    {
        Func<Task> act = () => _service.SearchAsync(
            new SearchOrdersQuery
            {
                Page = page,
                PageSize = pageSize
            },
            UserId);

        await act.Should().ThrowAsync<ValidationException>();
    }

    // =========================================================
    // SHIPPING
    // =========================================================

    [Fact]
    public async Task Ship_WithPendingOrder_ShouldSucceed()
    {
        var order = CreateOrder();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _repositoryMock
            .Setup(r => r.SetOrderStatusAsync(
                order.Id,
                OrderStatus.Pending,
                OrderStatus.Shipped,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.ShipAsync(order.Id);

        result.Should().BeTrue();
        VerifyCommittedOnce();
    }

    [Fact]
    public async Task Ship_WhenMissing_ShouldReturnFalse()
    {
        var result = await _service.ShipAsync(999);

        result.Should().BeFalse();
        VerifyNeverCommitted();
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Ship_WhenNotPending_ShouldThrow(
        OrderStatus status)
    {
        var order = CreateOrder(status: status);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Func<Task> act = () => _service.ShipAsync(order.Id);

        await act.Should().ThrowAsync<OrderException>();

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Ship_WhenStatusUpdateFails_ShouldThrow()
    {
        var order = CreateOrder();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Func<Task> act = () => _service.ShipAsync(order.Id);

        await act.Should().ThrowAsync<OrderException>();

        VerifyNeverCommitted();
    }

    // =========================================================
    // CANCELLATION
    // =========================================================

    [Fact]
    public async Task Cancel_WithPendingOrder_ShouldRestoreStock()
    {
        var order = CreateOrder();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _repositoryMock
            .Setup(r => r.SetOrderStatusAsync(
                order.Id,
                OrderStatus.Pending,
                OrderStatus.Cancelled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CancelAsync(order.Id);

        result.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.RestoreStockAsync(
                1,
                2,
                It.IsAny<CancellationToken>()),
            Times.Once());

        VerifyCommittedOnce();
    }

    [Fact]
    public async Task Cancel_WhenMissing_ShouldReturnFalse()
    {
        var result = await _service.CancelAsync(999);

        result.Should().BeFalse();
        VerifyNeverCommitted();
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Cancel_WhenNotPending_ShouldThrow(
        OrderStatus status)
    {
        var order = CreateOrder(status: status);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Func<Task> act = () => _service.CancelAsync(order.Id);

        await act.Should().ThrowAsync<OrderException>();

        _repositoryMock.Verify(
            r => r.RestoreStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never());

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Cancel_WhenStatusUpdateFails_ShouldNotRestoreStock()
    {
        var order = CreateOrder();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        Func<Task> act = () => _service.CancelAsync(order.Id);

        await act.Should().ThrowAsync<OrderException>();

        _repositoryMock.Verify(
            r => r.RestoreStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never());

        VerifyNeverCommitted();
    }

    [Fact]
    public async Task Cancel_WhenRestorationFails_ShouldNotCommit()
    {
        var order = CreateOrder();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(
                order.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _repositoryMock
            .Setup(r => r.SetOrderStatusAsync(
                order.Id,
                OrderStatus.Pending,
                OrderStatus.Cancelled,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _repositoryMock
            .Setup(r => r.RestoreStockAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Stock restoration failed."));

        Func<Task> act = () => _service.CancelAsync(order.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();

        VerifyNeverCommitted();

        _transactionMock.Verify(
            t => t.DisposeAsync(),
            Times.Once());
    }
}
