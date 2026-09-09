namespace BookCatalog.Application.Loans.Contracts;

public sealed record LoanDto(
    Guid Id,
    Guid BookId,
    string BookTitle,
    Guid UserId,
    string UserDisplayName,
    DateTimeOffset BorrowedAt,
    DateTimeOffset? ReturnedAt);
