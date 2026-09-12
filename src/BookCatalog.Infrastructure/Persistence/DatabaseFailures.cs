using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookCatalog.Infrastructure.Persistence;

public static class DatabaseFailures
{
    public static bool IsTransient(Exception exception)
    {
        // EF can wrap provider failures. Do not treat arbitrary application errors
        // or cancellation as database outages.
        while (exception is InvalidOperationException or DbUpdateException && exception.InnerException is not null)
            exception = exception.InnerException;

        return exception is NpgsqlException { IsTransient: true };
    }
}
