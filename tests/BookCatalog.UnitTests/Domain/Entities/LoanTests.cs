using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;

namespace BookCatalog.UnitTests.Domain.Entities;

public sealed class LoanTests
{
    private static readonly DateTimeOffset BorrowedAt = new(2026, 9, 9, 12, 0, 0, TimeSpan.FromHours(2));

    private static Loan CreateLoan() => Loan.Create(
        Book.Create("Test book", Author.Create("Test author"), "9780306406157", 2020, null),
        User.Create("Alex"), BorrowedAt);

    [Fact]
    public void Create_AssignsIdentityRelationshipsAndUtcTime()
    {
        var loan = CreateLoan();
        Assert.NotEqual(Guid.Empty, loan.Id);
        Assert.Equal(loan.Book.Id, loan.BookId);
        Assert.Equal(loan.User.Id, loan.UserId);
        Assert.Equal(BorrowedAt.ToUniversalTime(), loan.BorrowedAt);
        Assert.Equal(TimeSpan.Zero, loan.BorrowedAt.Offset);
        Assert.Null(loan.ReturnedAt);
    }

    [Fact]
    public void Create_RequiresBook() => Assert.Throws<ArgumentNullException>(() =>
        Loan.Create(null!, User.Create("Alex"), BorrowedAt));

    [Fact]
    public void Create_RequiresUser() => Assert.Throws<ArgumentNullException>(() =>
        Loan.Create(CreateLoan().Book, null!, BorrowedAt));

    [Fact]
    public void Return_PreservesHistoryAndNormalizesTime()
    {
        var loan = CreateLoan();
        var id = loan.Id;
        var returnedAt = BorrowedAt.AddHours(1).ToOffset(TimeSpan.FromHours(-3));

        loan.Return(loan.UserId, returnedAt);

        Assert.Equal(id, loan.Id);
        Assert.Equal(BorrowedAt, loan.BorrowedAt);
        Assert.Equal(returnedAt.ToUniversalTime(), loan.ReturnedAt);
        Assert.Equal(TimeSpan.Zero, loan.ReturnedAt!.Value.Offset);
    }

    [Fact]
    public void Return_WithDifferentBorrower_DoesNotChangeLoan()
    {
        var loan = CreateLoan();
        Assert.Throws<DomainConflictException>(() => loan.Return(Guid.NewGuid(), BorrowedAt.AddHours(1)));
        Assert.Null(loan.ReturnedAt);
    }

    [Fact]
    public void Return_WithEmptyBorrower_DoesNotChangeLoan()
    {
        var loan = CreateLoan();
        Assert.Throws<DomainValidationException>(() => loan.Return(Guid.Empty, BorrowedAt));
        Assert.Null(loan.ReturnedAt);
    }

    [Fact]
    public void Return_BeforeBorrowing_DoesNotChangeLoan()
    {
        var loan = CreateLoan();
        Assert.Throws<DomainValidationException>(() => loan.Return(loan.UserId, BorrowedAt.AddSeconds(-1)));
        Assert.Null(loan.ReturnedAt);
    }

    [Fact]
    public void Return_AtBorrowingTime_IsAllowed()
    {
        var loan = CreateLoan();
        loan.Return(loan.UserId, BorrowedAt);
        Assert.Equal(loan.BorrowedAt, loan.ReturnedAt);
    }

    [Fact]
    public void Return_Twice_PreservesFirstReturnTime()
    {
        var loan = CreateLoan();
        loan.Return(loan.UserId, BorrowedAt.AddHours(1));
        var firstReturn = loan.ReturnedAt;

        Assert.Throws<DomainConflictException>(() => loan.Return(loan.UserId, BorrowedAt.AddHours(2)));

        Assert.Equal(firstReturn, loan.ReturnedAt);
    }
}
