using CardShop.Application.Interfaces.Repositories;

namespace CardShop.Application.Interfaces.Services;

public interface ICartOperationExecutor
{
    Task<T> ExecuteAsync<T>(
        string userId,
        Func<ICartRepository, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}