using BookCatalog.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class EfCoreUnitOfWork(BookCatalogDbContext context) : IUnitOfWork
{
    private readonly BookCatalogDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            await operation(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        catch (DbUpdateException exception)
        {
            var translated = PersistenceErrors.Translate(exception);
            if (translated is not null) throw translated;
            throw;
        }
        finally
        {
            if (!committed) _context.ChangeTracker.Clear();
        }
    }
}
