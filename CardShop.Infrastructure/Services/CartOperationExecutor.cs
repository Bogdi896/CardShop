
using CardShop.Application.Common.Exceptions;
using CardShop.Application.Interfaces.Repositories;
using CardShop.Application.Interfaces.Services;
using CardShop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardShop.Infrastructure.Services;

public class CartOperationExecutor : ICartOperationExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CartOperationExecutor(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<T> ExecuteAsync<T>(
        string userId,
        Func<ICartRepository, CancellationToken, Task<T>> operation,
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
            // Every retry receives a fresh DbContext.
            await using var attemptScope =
                _scopeFactory.CreateAsyncScope();

            var context = attemptScope.ServiceProvider
                .GetRequiredService<CardShopDbContext>();

            var repository = attemptScope.ServiceProvider
                .GetRequiredService<ICartRepository>();

            // Keep transaction isolation consistent with checkout.
            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);

            // Acquire the same cart lock used by checkout.
            var locked = await repository.LockCartAsync(
                userId,
                cancellationToken);

            if (!locked)
            {
                throw new CartException(
                    "Cart was not found for this user.");
            }

            // Execute the business operation while holding the lock.
            var result = await operation(
                repository,
                cancellationToken);

            // Commit only if the operation succeeds.
            await transaction.CommitAsync(cancellationToken);

            return result;
        });
    }
}
