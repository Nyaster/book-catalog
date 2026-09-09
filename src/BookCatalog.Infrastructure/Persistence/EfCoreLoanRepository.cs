using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Application.Loans.Persistence;
using BookCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.Infrastructure.Persistence;

public sealed class EfCoreLoanRepository(BookCatalogDbContext context) : ILoanRepository
{
    private readonly BookCatalogDbContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public Task AddAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loan);
        cancellationToken.ThrowIfCancellationRequested();
        RequireTransaction();
        _context.Loans.Add(loan);
        return Task.CompletedTask;
    }

    public async Task<bool> TryReturnAsync(Guid loanId, Guid userId, DateTimeOffset returnedAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireTransaction();
        var affected = await _context.Loans
            .Where(loan => loan.Id == loanId && loan.UserId == userId && loan.ReturnedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(loan => loan.ReturnedAt, returnedAt), cancellationToken);
        return affected == 1;
    }

    public Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ReadLoans().SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);

    public Task<PagedResult<Loan>> GetByBookAsync(Guid bookId, PageQuery query, CancellationToken cancellationToken = default) =>
        GetPageAsync(ReadLoans().Where(loan => loan.BookId == bookId), query, cancellationToken);

    public Task<PagedResult<Loan>> GetByUserAsync(Guid userId, PageQuery query, CancellationToken cancellationToken = default) =>
        GetPageAsync(ReadLoans().Where(loan => loan.UserId == userId), query, cancellationToken);

    private IQueryable<Loan> ReadLoans() =>
        _context.Loans.AsNoTracking().Include(loan => loan.Book).Include(loan => loan.User);

    private static async Task<PagedResult<Loan>> GetPageAsync(
        IQueryable<Loan> loans, PageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var totalCount = await loans.CountAsync(cancellationToken);
        IReadOnlyList<Loan> items = query.Offset >= totalCount
            ? []
            : await loans.OrderByDescending(loan => loan.BorrowedAt).ThenByDescending(loan => loan.Id)
                .Skip((int)query.Offset).Take(query.PageSize).ToArrayAsync(cancellationToken);
        return new PagedResult<Loan>(items, query.Page, query.PageSize, totalCount);
    }

    private void RequireTransaction()
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Loan writes must run inside IUnitOfWork.ExecuteAsync.");
    }
}
