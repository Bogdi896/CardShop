
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardShop.IntegrationTests.Infrastructure;

public class CommitConfirmationFailureInterceptor
    : DbTransactionInterceptor
{
    private int _armed;
    private int _failureCount;

    public int FailureCount =>
        Volatile.Read(ref _failureCount);

    public void Arm()
    {
        Interlocked.Exchange(ref _armed, 1);
    }

    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(
            ref _armed, 0, 1) == 1)
        {
            Interlocked.Increment(ref _failureCount);

            throw new TimeoutException(
                "TEST ONLY: Commit succeeded, but its " +
                "confirmation was lost.");
        }

        return Task.CompletedTask;
    }
}
