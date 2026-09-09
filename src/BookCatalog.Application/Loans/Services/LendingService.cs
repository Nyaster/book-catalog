using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Application.Books.Persistence;
using BookCatalog.Application.Common;
using BookCatalog.Application.Common.Persistence;
using BookCatalog.Application.Loans.Contracts;
using BookCatalog.Application.Loans.Exceptions;
using BookCatalog.Application.Loans.Persistence;
using BookCatalog.Application.Users.Exceptions;
using BookCatalog.Application.Users.Persistence;
using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;
using BookCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BookCatalog.Application.Loans.Services;

public sealed class LendingService(
    IBookRepository books,
    IUserRepository users,
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<LendingService> logger) : ILendingService
{
    private readonly IBookRepository _books = books ?? throw new ArgumentNullException(nameof(books));
    private readonly IUserRepository _users = users ?? throw new ArgumentNullException(nameof(users));
    private readonly ILoanRepository _loans = loans ?? throw new ArgumentNullException(nameof(loans));
    private readonly IUnitOfWork _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<LendingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<LoanDto> BorrowAsync(Guid bookId, Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EntityValidation.RequireId(bookId, "Book ID");
        EntityValidation.RequireId(userId, "User ID");
        var book = await GetRequiredBookAsync(bookId, cancellationToken);
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        var loan = Loan.Create(book, user, _timeProvider.GetUtcNow());

        await _unitOfWork.ExecuteAsync(async token =>
        {
            if (!await _books.TryBorrowAsync(bookId, token))
            {
                await GetRequiredBookAsync(bookId, token);
                throw new DomainConflictException("This book is already borrowed.");
            }
            await _loans.AddAsync(loan, token);
        }, cancellationToken);
        _logger.LogInformation("User {UserId} borrowed book {BookId}; loan {LoanId}.", userId, bookId, loan.Id);
        return MapToDto(loan);
    }

    public async Task<LoanDto> ReturnAsync(Guid loanId, Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EntityValidation.RequireId(loanId, "Loan ID");
        EntityValidation.RequireId(userId, "User ID");
        var loan = await GetRequiredLoanAsync(loanId, cancellationToken);
        loan.Return(userId, _timeProvider.GetUtcNow());

        await _unitOfWork.ExecuteAsync(async token =>
        {
            // Close this loan first: a repeated old return must never release a newer loan.
            if (!await _loans.TryReturnAsync(loan.Id, userId, loan.ReturnedAt!.Value, token))
                throw new DomainConflictException("This loan has already been returned or its borrower has changed.");
            if (!await _books.TryReleaseAsync(loan.BookId, token))
                throw new InvalidOperationException("The book availability does not match its active loan.");
        }, cancellationToken);
        _logger.LogInformation("User {UserId} returned book {BookId}; loan {LoanId}.", userId, loan.BookId, loanId);
        return MapToDto(loan);
    }

    public async Task<LoanDto> GetByIdAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MapToDto(await GetRequiredLoanAsync(loanId, cancellationToken));
    }

    public async Task<PagedResult<LoanDto>> GetByBookAsync(
        Guid bookId, PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await GetRequiredBookAsync(bookId, cancellationToken);
        return MapPage(await _loans.GetByBookAsync(bookId, query, cancellationToken));
    }

    public async Task<PagedResult<LoanDto>> GetByUserAsync(
        Guid userId, PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await GetRequiredUserAsync(userId, cancellationToken);
        return MapPage(await _loans.GetByUserAsync(userId, query, cancellationToken));
    }

    private async Task<Book> GetRequiredBookAsync(Guid id, CancellationToken cancellationToken) =>
        await _books.GetByIdAsync(id, cancellationToken) ?? throw new BookNotFoundException(id);

    private async Task<User> GetRequiredUserAsync(Guid id, CancellationToken cancellationToken) =>
        await _users.GetByIdAsync(id, cancellationToken) ?? throw new UserNotFoundException(id);

    private async Task<Loan> GetRequiredLoanAsync(Guid id, CancellationToken cancellationToken) =>
        await _loans.GetByIdAsync(id, cancellationToken) ?? throw new LoanNotFoundException(id);

    private static LoanDto MapToDto(Loan loan) => new(
        loan.Id, loan.BookId, loan.Book.Title, loan.UserId, loan.User.DisplayName, loan.BorrowedAt, loan.ReturnedAt);

    private static PagedResult<LoanDto> MapPage(PagedResult<Loan> page) => new(
        page.Items.Select(MapToDto).ToArray(), page.Page, page.PageSize, page.TotalCount);
}
