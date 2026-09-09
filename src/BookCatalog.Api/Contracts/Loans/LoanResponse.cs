namespace BookCatalog.Api.Contracts.Loans;

public sealed record LoanResponse(
    Guid Id,
    Guid BookId,
    string BookTitle,
    Guid UserId,
    string UserDisplayName,
    DateTimeOffset BorrowedAt,
    DateTimeOffset? ReturnedAt);
