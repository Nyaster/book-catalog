namespace BookCatalog.Application.Common.Persistence;

public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
