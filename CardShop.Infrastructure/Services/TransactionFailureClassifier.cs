using Microsoft.Data.SqlClient;

namespace CardShop.Infrastructure.Services;

public static class TransactionFailureClassifier
{
    public static bool IsPotentialCommitFailure(Exception exception)
    {
        if (exception is TimeoutException)
        {
            return true;
        }

        if (exception is SqlException sqlException)
        {
            return IsRelevantSqlError(sqlException);
        }

        if (exception.InnerException is not null)
        {
            return IsPotentialCommitFailure(
                exception.InnerException);
        }

        return false;
    }

    private static bool IsRelevantSqlError(SqlException exception)
    {
        foreach (SqlError error in exception.Errors)
        {
            switch (error.Number)
            {
                case 1205: // Deadlock
                case -2:   // SQL command timeout
                case 64:   // Network connection failure
                case 233:  // Connection initialization failure
                case 10053: // Connection aborted
                case 10054: // Connection reset
                case 10060: // Connection timeout
                    return true;
            }
        }

        return false;
    }
}