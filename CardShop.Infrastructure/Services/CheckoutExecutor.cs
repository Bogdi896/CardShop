
using System.Data;
using CardShop.Application.DTOs.Orders;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.Mappings;
using CardShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.Infrastructure.Services;

public class CheckoutExecutor : ICheckoutExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CheckoutExecutor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // =========================================================
    // GENERAL ORDER OPERATIONS
    // =========================================================

    public async Task<T> ExecuteAsync<T>(
        Func<IOrderRepository, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var strategyScope =
            _scopeFactory.CreateAsyncScope();

        var strategyContext = strategyScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var strategy = strategyContext.Database
            .CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var attemptScope =
                _scopeFactory.CreateAsyncScope();

            var repository = attemptScope.ServiceProvider
                .GetRequiredService<IOrderRepository>();

            return await operation(
                repository,
                cancellationToken);
        });
    }

    // =========================================================
    // CHECKOUT WITH COMMIT VERIFICATION
    // =========================================================

    public async Task<OrderResponse> ExecuteCheckoutAsync(
        string userId,
        Guid checkoutId,
        Func<IOrderRepository, CancellationToken, Task<OrderResponse>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var strategyScope =
            _scopeFactory.CreateAsyncScope();

        var strategyContext = strategyScope.ServiceProvider
            .GetRequiredService<CardShopDbContext>();

        var strategy = strategyContext.Database
            .CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Each retry uses a fresh DbContext.
            await using var attemptScope =
                _scopeFactory.CreateAsyncScope();

            var context = attemptScope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            var repository = attemptScope.ServiceProvider
                .GetRequiredService<IOrderRepository>();

            // Recover a checkout committed by an earlier attempt.
            var existingOrder =
                await repository.GetByCheckoutIdAsync(
                    userId,
                    checkoutId,
                    cancellationToken);

            if (existingOrder is not null)
            {
                return OrderMapper.ToDto(existingOrder);
            }

            OrderResponse result;
            var commitAttempted = false;

            try
            {
                await using var transaction =
                    await context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                result = await operation(
                    repository,
                    cancellationToken);

                commitAttempted = true;

                await transaction.CommitAsync(
                    cancellationToken);

                return result;
            }
            catch (Exception exception) when (
                commitAttempted &&
                !cancellationToken.IsCancellationRequested &&
                TransactionFailureClassifier
                    .IsPotentialCommitFailure(exception))
            {
                // Commit confirmation may have been lost.
                // Verify using a separate DbContext.
                await using var verificationScope =
                    _scopeFactory.CreateAsyncScope();

                var verificationRepository =
                    verificationScope.ServiceProvider
                        .GetRequiredService<IOrderRepository>();

                var committedOrder =
                    await verificationRepository.GetByCheckoutIdAsync(
                        userId,
                        checkoutId,
                        cancellationToken);

                if (committedOrder is not null)
                {
                    return OrderMapper.ToDto(committedOrder);
                }

                // Let EF Core's execution strategy decide
                // whether the original error is retryable.
                throw;
            }
        });
    }
}
