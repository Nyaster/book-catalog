using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BookCatalog.IntegrationTests.Infrastructure;

internal sealed class DatabasePauseInterceptor : DbCommandInterceptor
{
    private Func<string, bool>? _matches;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;
    public void Arm(Func<string, bool> matches) => _matches = matches;
    public void Release() => _release.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (_matches?.Invoke(command.CommandText) == true)
        {
            _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }
}
