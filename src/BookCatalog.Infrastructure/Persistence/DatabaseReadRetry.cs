using System.Diagnostics;
using System.Transactions;
using Microsoft.Extensions.Logging;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class DatabaseReadRetry(BookCatalogDbContext context, ILogger<DatabaseReadRetry> logger)
{
    public async Task<T> ExecuteAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await read(cancellationToken);
            }
            catch (Exception exception) when (
                attempt < 3 &&
                !cancellationToken.IsCancellationRequested &&
                context.Database.CurrentTransaction is null &&
                Transaction.Current is null &&
                DatabaseFailures.IsTransient(exception))
            {
                var delay = TimeSpan.FromMilliseconds(250 * attempt + Random.Shared.Next(101));
                logger.LogWarning(
                    "Database read failed temporarily. Operation: {Operation}; Attempt: {Attempt}; DelayMs: {DelayMs}; TraceId: {TraceId}",
                    operationName, attempt, delay.TotalMilliseconds, Activity.Current?.TraceId.ToString());
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
