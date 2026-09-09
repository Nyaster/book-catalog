using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;

namespace BookCatalog.Application.Loans.Persistence;

public interface ILoanRepository
{
    // Stages a new loan. IUnitOfWork saves it with the book's availability change.
    Task AddAsync(Loan loan, CancellationToken cancellationToken = default);

    Task<bool> TryReturnAsync(Guid loanId, Guid userId, DateTimeOffset returnedAt,
        CancellationToken cancellationToken = default);

    Task<Loan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Loan>> GetByBookAsync(Guid bookId, PageQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<Loan>> GetByUserAsync(Guid userId, PageQuery query, CancellationToken cancellationToken = default);
}
