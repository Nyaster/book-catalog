using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Application.Loans.Contracts;

namespace BookCatalog.Application.Loans.Services;

public interface ILendingService
{
    Task<LoanDto> BorrowAsync(Guid bookId, Guid userId, CancellationToken cancellationToken = default);
    Task<LoanDto> ReturnAsync(Guid loanId, Guid userId, CancellationToken cancellationToken = default);
    Task<LoanDto> GetByIdAsync(Guid loanId, CancellationToken cancellationToken = default);
    Task<PagedResult<LoanDto>> GetByBookAsync(Guid bookId, PageQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<LoanDto>> GetByUserAsync(Guid userId, PageQuery query, CancellationToken cancellationToken = default);
}
