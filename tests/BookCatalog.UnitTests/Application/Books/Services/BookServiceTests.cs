using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Books.Exceptions;
using BookCatalog.Application.Books.Persistence;
using BookCatalog.Application.Books.Services;
using BookCatalog.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace BookCatalog.UnitTests.Application.Books.Services;

public sealed class BookServiceTests
{
    private const string ValidIsbn13 = "9780306406157";

    [Fact]
    public async Task CreateAsync_WhenIsbnIsAvailable_AddsBookAndReturnsDto()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var repository = CreateRepositoryMock();
        Book? addedBook = null;

        repository
            .Setup(repository => repository.IsIsbnInUseAsync(ValidIsbn13, null, cancellationToken))
            .ReturnsAsync(false);
        repository
            .Setup(repository => repository.AddAsync(It.IsAny<Book>(), cancellationToken))
            .Callback<Book, CancellationToken>((book, _) => addedBook = book)
            .Returns(Task.CompletedTask);

        var service = CreateService(repository);

        var result = await service.CreateAsync(
            new CreateBookCommand(
                "  Clean Code  ",
                "  Robert C. Martin  ",
                "978-0-306-40615-7",
                2008,
                "  A practical book about writing code.  "),
            cancellationToken);

        Assert.NotNull(addedBook);
        Assert.Equal(addedBook!.Id, result.Id);
        Assert.Equal("Clean Code", result.Title);
        Assert.Equal("Robert C. Martin", result.Author);
        Assert.Equal(ValidIsbn13, result.Isbn);
        Assert.Equal(2008, result.PublicationYear);
        Assert.Equal("A practical book about writing code.", result.Description);
        repository.Verify(
            repository => repository.IsIsbnInUseAsync(ValidIsbn13, null, cancellationToken),
            Times.Once);
        repository.Verify(
            repository => repository.AddAsync(addedBook!, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenIsbnAlreadyExists_ThrowsDuplicateIsbnException_AndDoesNotAdd()
    {
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.IsIsbnInUseAsync(ValidIsbn13, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<DuplicateIsbnException>(() =>
            service.CreateAsync(CreateCommand()));

        Assert.Equal(ValidIsbn13, exception.Isbn);
        repository.Verify(
            repository => repository.AddAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        var service = CreateService(CreateRepositoryMock());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateAsync(null!));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact]
    public async Task GetPageAsync_ForwardsRequestAndMapsDtosWithPaginationMetadata()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var repository = CreateRepositoryMock();
        var pageRequest = new BookListQuery(2, 2);
        IReadOnlyList<Book> books = new List<Book>
        {
            CreateBook("Alpha", "Anne", "9780306406157"),
            CreateBook("Zebra", "Author B", "9780306406158")
        };
        repository
            .Setup(repository => repository.GetPageAsync(pageRequest, cancellationToken))
            .ReturnsAsync(new PagedResult<Book>(books, 2, 2, 5));
        var service = CreateService(repository);

        var result = await service.GetPageAsync(pageRequest, cancellationToken);

        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
        Assert.Collection(
            result.Items,
            book =>
            {
                Assert.Equal("Alpha", book.Title);
                Assert.Equal("Anne", book.Author);
            },
            book =>
            {
                Assert.Equal("Zebra", book.Title);
                Assert.Equal("Author B", book.Author);
            });
        repository.Verify(repository => repository.GetPageAsync(pageRequest, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ForwardsBookFilter()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var repository = CreateRepositoryMock();
        var query = new BookListQuery(
            2,
            10,
            new BookFilter(
                "clean",
                "martin",
                "978013",
                2008,
                2010,
                2000));
        repository
            .Setup(repository => repository.GetPageAsync(query, cancellationToken))
            .ReturnsAsync(new PagedResult<Book>([], 2, 10, 0));
        var service = CreateService(repository);

        await service.GetPageAsync(query, cancellationToken);

        repository.Verify(repository => repository.GetPageAsync(query, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_WhenBookListQueryIsNull_ThrowsArgumentNullException()
    {
        var service = CreateService(CreateRepositoryMock());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetPageAsync(null!));

        Assert.Equal("pageRequest", exception.ParamName);
    }

    [Theory]
    [InlineData(0, 20, "page")]
    [InlineData(1, 0, "pageSize")]
    public void BookListQuery_WhenPageOrPageSizeIsLessThanOne_ThrowsArgumentOutOfRangeException(
        int page,
        int pageSize,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BookListQuery(page, pageSize));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public async Task GetByIdAsync_WhenBookExists_ReturnsDto()
    {
        var book = CreateBook("Clean Code", "Robert C. Martin", ValidIsbn13);
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.GetByIdAsync(book.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(book);
        var service = CreateService(repository);

        var result = await service.GetByIdAsync(book.Id);

        Assert.Equal(book.Id, result.Id);
        Assert.Equal(book.Title, result.Title);
        Assert.Equal(book.Author, result.Author);
        Assert.Equal(book.Isbn, result.Isbn);
        Assert.Equal(book.PublicationYear, result.PublicationYear);
        Assert.Equal(book.Description, result.Description);
    }

    [Fact]
    public async Task GetByIdAsync_WhenBookDoesNotExist_ThrowsBookNotFoundException()
    {
        var id = Guid.NewGuid();
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<Book?>(null));
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BookNotFoundException>(() => service.GetByIdAsync(id));

        Assert.Equal(id, exception.BookId);
    }

    [Fact]
    public async Task UpdateAsync_WhenBookExistsAndIsbnIsAvailable_UpdatesBookAndReturnsDto()
    {
        var cancellationToken = new CancellationTokenSource().Token;
        var existingBook = CreateBook("Original title", "Original author", ValidIsbn13);
        var command = new UpdateBookCommand(
            "  Refactoring  ",
            "  Martin Fowler  ",
            "0-8044-2957-x",
            1999,
            "  Improving the design of existing code.  ");
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.GetByIdAsync(existingBook.Id, cancellationToken))
            .ReturnsAsync(existingBook);
        repository
            .Setup(repository => repository.IsIsbnInUseAsync("080442957X", existingBook.Id, cancellationToken))
            .ReturnsAsync(false);
        repository
            .Setup(repository => repository.UpdateAsync(existingBook, cancellationToken))
            .Returns(Task.CompletedTask);
        var service = CreateService(repository);

        var result = await service.UpdateAsync(existingBook.Id, command, cancellationToken);

        Assert.Equal(existingBook.Id, result.Id);
        Assert.Equal("Refactoring", result.Title);
        Assert.Equal("Martin Fowler", result.Author);
        Assert.Equal("080442957X", result.Isbn);
        Assert.Equal(1999, result.PublicationYear);
        Assert.Equal("Improving the design of existing code.", result.Description);
        repository.Verify(
            repository => repository.IsIsbnInUseAsync("080442957X", existingBook.Id, cancellationToken),
            Times.Once);
        repository.Verify(repository => repository.UpdateAsync(existingBook, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenBookDoesNotExist_ThrowsBookNotFoundException_AndDoesNotCheckIsbn()
    {
        var id = Guid.NewGuid();
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<Book?>(null));
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BookNotFoundException>(() =>
            service.UpdateAsync(id, CreateUpdateCommand()));

        Assert.Equal(id, exception.BookId);
        repository.Verify(
            repository => repository.IsIsbnInUseAsync(
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        var service = CreateService(CreateRepositoryMock());

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.UpdateAsync(Guid.NewGuid(), null!));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact]
    public async Task UpdateAsync_WhenIsbnBelongsToAnotherBook_ThrowsDuplicateIsbnException_AndDoesNotUpdate()
    {
        var existingBook = CreateBook("Original title", "Original author", ValidIsbn13);
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.GetByIdAsync(existingBook.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingBook);
        repository
            .Setup(repository =>
                repository.IsIsbnInUseAsync("080442957X", existingBook.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<DuplicateIsbnException>(() =>
            service.UpdateAsync(existingBook.Id, CreateUpdateCommand()));

        Assert.Equal("080442957X", exception.Isbn);
        Assert.Equal("Original title", existingBook.Title);
        Assert.Equal("Original author", existingBook.Author);
        Assert.Equal(ValidIsbn13, existingBook.Isbn);
        repository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Book>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenRepositoryDeletesBook_CompletesAndVerifiesDeleteCall()
    {
        var id = Guid.NewGuid();
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService(repository);

        await service.DeleteAsync(id);

        repository.Verify(
            repository => repository.DeleteAsync(id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenRepositoryCannotDeleteBook_ThrowsBookNotFoundException()
    {
        var id = Guid.NewGuid();
        var repository = CreateRepositoryMock();
        repository
            .Setup(repository => repository.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BookNotFoundException>(() => service.DeleteAsync(id));

        Assert.Equal(id, exception.BookId);
    }

    [Fact]
    public async Task
        CreateAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException_AndDoesNotAccessRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var repository = CreateRepositoryMock();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CreateAsync(CreateCommand(), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        GetPageAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException_AndDoesNotAccessRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var repository = CreateRepositoryMock();
        var service = CreateService(repository);
        var pageRequest = new BookListQuery(1, 20);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetPageAsync(pageRequest, cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        GetByIdAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException_AndDoesNotAccessRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var repository = CreateRepositoryMock();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetByIdAsync(Guid.NewGuid(), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        UpdateAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException_AndDoesNotAccessRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var repository = CreateRepositoryMock();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.UpdateAsync(Guid.NewGuid(), CreateUpdateCommand(), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task
        DeleteAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException_AndDoesNotAccessRepository()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var repository = CreateRepositoryMock();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.DeleteAsync(Guid.NewGuid(), cancellationTokenSource.Token));

        Assert.Equal(cancellationTokenSource.Token, exception.CancellationToken);
        repository.VerifyNoOtherCalls();
    }

    private static Mock<IBookRepository> CreateRepositoryMock()
    {
        return new Mock<IBookRepository>(MockBehavior.Strict);
    }

    private static BookService CreateService(Mock<IBookRepository> repository)
    {
        return new BookService(repository.Object, new Mock<ILogger<BookService>>().Object);
    }

    private static CreateBookCommand CreateCommand()
    {
        return new CreateBookCommand(
            "Clean Code",
            "Robert C. Martin",
            ValidIsbn13,
            2008,
            "A practical book about writing code.");
    }

    private static UpdateBookCommand CreateUpdateCommand()
    {
        return new UpdateBookCommand(
            "Refactoring",
            "Martin Fowler",
            "0-8044-2957-x",
            1999,
            "Improving the design of existing code.");
    }

    private static Book CreateBook(string title, string author, string isbn)
    {
        return Book.Create(title, author, isbn, 2000, "Description");
    }
}
