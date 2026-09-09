using BookCatalog.Domain.Exceptions;
using BookCatalog.Domain.ValueObjects;

namespace BookCatalog.Domain.Entities;

public sealed class Loan
{
    public Guid Id { get; private set; }
    public Guid BookId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset BorrowedAt { get; private set; }
    public DateTimeOffset? ReturnedAt { get; private set; }
    public Book Book { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private Loan() { }

    public static Loan Create(Book book, User user, DateTimeOffset borrowedAt)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(user);

        return new Loan
        {
            Id = Guid.NewGuid(),
            BookId = book.Id,
            UserId = user.Id,
            Book = book,
            User = user,
            BorrowedAt = borrowedAt.ToUniversalTime()
        };
    }

    public void Return(Guid userId, DateTimeOffset returnedAt)
    {
        EntityValidation.RequireId(userId, "User ID");
        if (userId != UserId)
            throw new DomainConflictException("Only the loan's borrower can return this book.");
        if (ReturnedAt is not null)
            throw new DomainConflictException("This loan has already been returned.");
        if (returnedAt < BorrowedAt)
            throw new DomainValidationException("Return time cannot precede borrowing time.");

        ReturnedAt = returnedAt.ToUniversalTime();
    }
}
