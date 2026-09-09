    using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Application.Books.Persistence;
using BookCatalog.Application.Common;
using BookCatalog.Application.Common.Persistence;
using BookCatalog.Application.Loans.Exceptions;
using BookCatalog.Application.Loans.Persistence;
using BookCatalog.Application.Loans.Services;
using BookCatalog.Application.Users.Exceptions;
using BookCatalog.Application.Users.Persistence;
using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BookCatalog.UnitTests.Application.Loans.Services;

public sealed class LendingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly Book _book = Book.Create("Test book", Author.Create("Test author"), "9780306406157", 2020, null);
    private readonly User _user = User.Create("Alex");
    private readonly Mock<IBookRepository> _books = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<ILoanRepository> _loans = new(MockBehavior.Strict);
    private readonly RecordingUnitOfWork _work = new();

    private LendingService CreateService() => new(_books.Object, _users.Object, _loans.Object, _work,
        new FixedTimeProvider(Now), NullLogger<LendingService>.Instance);

    private void SetupBorrower()
    {
        _books.Setup(r => r.GetByIdAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_book);
        _users.Setup(r => r.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
    }

    private Loan SetupLoan()
    {
        var loan = Loan.Create(_book, _user, Now.AddDays(-1));
        _loans.Setup(r => r.GetByIdAsync(loan.Id, It.IsAny<CancellationToken>())).ReturnsAsync(loan);
        return loan;
    }

    [Fact]
    public async Task Borrow_ClaimsBookThenStagesLoanInsideOneUnitOfWork()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        SetupBorrower();
        var order = new MockSequence();
        Loan? staged = null;
        _books.InSequence(order).Setup(r => r.TryBorrowAsync(_book.Id, token))
            .Callback(() => Assert.True(_work.IsExecuting)).ReturnsAsync(true);
        _loans.InSequence(order).Setup(r => r.AddAsync(It.IsAny<Loan>(), token))
            .Callback<Loan, CancellationToken>((loan, _) =>
            {
                Assert.True(_work.IsExecuting);
                staged = loan;
            }).Returns(Task.CompletedTask);

        var response = await CreateService().BorrowAsync(_book.Id, _user.Id, token);

        Assert.NotNull(staged);
        Assert.Equal(staged.Id, response.Id);
        Assert.Equal(_book.Id, response.BookId);
        Assert.Equal(_book.Title, response.BookTitle);
        Assert.Equal(_user.Id, response.UserId);
        Assert.Equal(_user.DisplayName, response.UserDisplayName);
        Assert.Equal(Now, response.BorrowedAt);
        Assert.Null(response.ReturnedAt);
        Assert.Equal(1, _work.Calls);
        Assert.Equal(1, _work.Completed);
        Assert.Equal(token, _work.Token);
        _books.Verify(r => r.TryBorrowAsync(_book.Id, token), Times.Once);
        _loans.Verify(r => r.AddAsync(staged, token), Times.Once);
    }

    [Fact]
    public async Task Borrow_WhenBookMissing_DoesNotStartUnitOfWork()
    {
        _books.Setup(r => r.GetByIdAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Book?)null);

        await Assert.ThrowsAsync<BookNotFoundException>(() => CreateService().BorrowAsync(_book.Id, _user.Id));

        Assert.Equal(0, _work.Calls);
        _users.VerifyNoOtherCalls();
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Borrow_WhenUserMissing_DoesNotClaimBook()
    {
        _books.Setup(r => r.GetByIdAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_book);
        _users.Setup(r => r.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(() => CreateService().BorrowAsync(_book.Id, _user.Id));

        Assert.Equal(0, _work.Calls);
        _books.Verify(r => r.TryBorrowAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _loans.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Borrow_WithEmptyId_DoesNotAccessStorage(bool emptyBook)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => CreateService().BorrowAsync(
            emptyBook ? Guid.Empty : _book.Id, emptyBook ? _user.Id : Guid.Empty));

        Assert.Equal(0, _work.Calls);
        _books.VerifyNoOtherCalls();
        _users.VerifyNoOtherCalls();
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Borrow_WhenAnotherRequestClaimsBook_DoesNotStageLoan()
    {
        SetupBorrower();
        _books.Setup(r => r.TryBorrowAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<DomainConflictException>(() => CreateService().BorrowAsync(_book.Id, _user.Id));

        Assert.Equal(1, _work.Calls);
        Assert.Equal(0, _work.Completed);
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Borrow_WhenBookDeletedAfterLookup_ReturnsNotFound()
    {
        _books.SetupSequence(r => r.GetByIdAsync(_book.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_book).ReturnsAsync((Book?)null);
        _users.Setup(r => r.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _books.Setup(r => r.TryBorrowAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<BookNotFoundException>(() => CreateService().BorrowAsync(_book.Id, _user.Id));

        Assert.Equal(0, _work.Completed);
        _loans.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Borrow_WhenLoanStagingFails_OperationDoesNotComplete()
    {
        SetupBorrower();
        _books.Setup(r => r.TryBorrowAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var failure = new InvalidOperationException("Staging failed.");
        _loans.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().BorrowAsync(_book.Id, _user.Id));

        Assert.Same(failure, error);
        Assert.Equal(0, _work.Completed);
    }

    [Fact]
    public async Task Borrow_WhenCommitRejectsDuplicateLoan_PropagatesConflict()
    {
        SetupBorrower();
        _books.Setup(r => r.TryBorrowAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _loans.Setup(r => r.AddAsync(It.IsAny<Loan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _work.CommitFailure = new DomainConflictException("Another active loan exists.");

        var error = await Assert.ThrowsAsync<DomainConflictException>(() => CreateService().BorrowAsync(_book.Id, _user.Id));

        Assert.Same(_work.CommitFailure, error);
        Assert.Equal(0, _work.Completed);
    }

    [Fact]
    public async Task Return_ClosesSpecificLoanThenReleasesBookInsideUnitOfWork()
    {
        var loan = SetupLoan();
        var order = new MockSequence();
        _loans.InSequence(order).Setup(r => r.TryReturnAsync(loan.Id, _user.Id, Now, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(_work.IsExecuting)).ReturnsAsync(true);
        _books.InSequence(order).Setup(r => r.TryReleaseAsync(_book.Id, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(_work.IsExecuting)).ReturnsAsync(true);

        var response = await CreateService().ReturnAsync(loan.Id, _user.Id);

        Assert.Equal(loan.Id, response.Id);
        Assert.Equal(Now, response.ReturnedAt);
        Assert.Equal(loan.BorrowedAt, response.BorrowedAt);
        Assert.Equal(1, _work.Calls);
        Assert.Equal(1, _work.Completed);
    }

    [Fact]
    public async Task Return_WhenLoanMissing_DoesNotStartUnitOfWork()
    {
        var id = Guid.NewGuid();
        _loans.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Loan?)null);

        await Assert.ThrowsAsync<LoanNotFoundException>(() => CreateService().ReturnAsync(id, _user.Id));

        Assert.Equal(0, _work.Calls);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Return_WithWrongBorrower_DoesNotWrite()
    {
        var loan = SetupLoan();

        await Assert.ThrowsAsync<DomainConflictException>(() => CreateService().ReturnAsync(loan.Id, Guid.NewGuid()));

        Assert.Null(loan.ReturnedAt);
        Assert.Equal(0, _work.Calls);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Return_OfAlreadyReturnedLoan_DoesNotReleaseBook()
    {
        var loan = SetupLoan();
        loan.Return(_user.Id, Now.AddHours(-1));
        var firstReturn = loan.ReturnedAt;

        await Assert.ThrowsAsync<DomainConflictException>(() => CreateService().ReturnAsync(loan.Id, _user.Id));

        Assert.Equal(firstReturn, loan.ReturnedAt);
        Assert.Equal(0, _work.Calls);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Return_WhenAnotherRequestReturnedLoan_DoesNotReleaseBook()
    {
        var loan = SetupLoan();
        _loans.Setup(r => r.TryReturnAsync(loan.Id, _user.Id, Now, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<DomainConflictException>(() => CreateService().ReturnAsync(loan.Id, _user.Id));

        Assert.Equal(0, _work.Completed);
        _books.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Return_WhenBookReleaseFails_OperationDoesNotComplete()
    {
        var loan = SetupLoan();
        _loans.Setup(r => r.TryReturnAsync(loan.Id, _user.Id, Now, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _books.Setup(r => r.TryReleaseAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ReturnAsync(loan.Id, _user.Id));

        Assert.Equal(0, _work.Completed);
    }

    [Fact]
    public async Task GetById_MapsLoanWithoutStartingUnitOfWork()
    {
        var loan = SetupLoan();
        var dto = await CreateService().GetByIdAsync(loan.Id);
        Assert.Equal(loan.Id, dto.Id);
        Assert.Equal(_user.DisplayName, dto.UserDisplayName);
        Assert.Equal(_book.Title, dto.BookTitle);
        Assert.Equal(0, _work.Calls);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("user")]
    public async Task History_IncludesActiveAndReturnedLoansAndPagination(string parent)
    {
        SetupBorrower();
        var active = Loan.Create(_book, _user, Now);
        var returned = Loan.Create(_book, _user, Now.AddDays(-1));
        returned.Return(_user.Id, Now.AddHours(-1));
        var query = new PageQuery(2, 2);
        var page = new PagedResult<Loan>([active, returned], 2, 2, 5);
        if (parent == "book")
            _loans.Setup(r => r.GetByBookAsync(_book.Id, query, It.IsAny<CancellationToken>())).ReturnsAsync(page);
        else
            _loans.Setup(r => r.GetByUserAsync(_user.Id, query, It.IsAny<CancellationToken>())).ReturnsAsync(page);

        var service = CreateService();
        var response = parent == "book"
            ? await service.GetByBookAsync(_book.Id, query)
            : await service.GetByUserAsync(_user.Id, query);

        Assert.Collection(response.Items, item => Assert.Null(item.ReturnedAt), item => Assert.Equal(returned.ReturnedAt, item.ReturnedAt));
        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
        Assert.Equal(5, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(0, _work.Calls);
    }

    [Theory]
    [InlineData("book")]
    [InlineData("user")]
    public async Task History_WhenParentMissing_DoesNotQueryLoans(string parent)
    {
        var service = CreateService();
        if (parent == "book")
        {
            _books.Setup(r => r.GetByIdAsync(_book.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Book?)null);
            await Assert.ThrowsAsync<BookNotFoundException>(() => service.GetByBookAsync(_book.Id, new PageQuery()));
        }
        else
        {
            _users.Setup(r => r.GetByIdAsync(_user.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
            await Assert.ThrowsAsync<UserNotFoundException>(() => service.GetByUserAsync(_user.Id, new PageQuery()));
        }
        _loans.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("borrow")]
    [InlineData("return")]
    [InlineData("get")]
    [InlineData("book")]
    [InlineData("user")]
    public async Task CancelledOperation_DoesNotAccessStorage(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        var service = CreateService();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "borrow": await service.BorrowAsync(_book.Id, _user.Id, token); break;
                case "return": await service.ReturnAsync(Guid.NewGuid(), _user.Id, token); break;
                case "get": await service.GetByIdAsync(Guid.NewGuid(), token); break;
                case "book": await service.GetByBookAsync(_book.Id, new PageQuery(), token); break;
                case "user": await service.GetByUserAsync(_user.Id, new PageQuery(), token); break;
            }
        });
        Assert.Equal(0, _work.Calls);
        _books.VerifyNoOtherCalls();
        _users.VerifyNoOtherCalls();
        _loans.VerifyNoOtherCalls();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Exercises service orchestration only. This test double does not simulate SQL rollback.
    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Calls { get; private set; }
        public int Completed { get; private set; }
        public bool IsExecuting { get; private set; }
        public CancellationToken Token { get; private set; }
        public Exception? CommitFailure { get; set; }

        public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            Calls++;
            Token = cancellationToken;
            IsExecuting = true;
            try
            {
                await operation(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (CommitFailure is not null) throw CommitFailure;
                Completed++;
            }
            finally { IsExecuting = false; }
        }
    }
}
