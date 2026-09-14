using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace BookCatalog.IntegrationTests.Infrastructure;

internal sealed class DatabaseFailureInterceptor : DbCommandInterceptor
{
    private Func<string, bool>? _matches;
    private int _failures;
    private int _firstFailure;
    private int _attempts;

    public int Attempts => Volatile.Read(ref _attempts);

    public void Arm(Func<string, bool> matches, int failures, int firstFailure = 1)
    {
        _attempts = 0;
        _failures = failures;
        _firstFailure = firstFailure;
        _matches = matches;
    }

    public void Disarm() => _matches = null;

    private void FailIfRequested(DbCommand command)
    {
        if (_matches?.Invoke(command.CommandText) != true) return;
        var attempt = Interlocked.Increment(ref _attempts);
        if (attempt >= _firstFailure && attempt - _firstFailure < _failures)
            throw new NpgsqlException("Test-only private database details", new IOException("Connection interrupted"));
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        FailIfRequested(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FailIfRequested(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        FailIfRequested(command);
        return ValueTask.FromResult(result);
    }
}