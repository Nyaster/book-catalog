using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;

namespace BookCatalog.UnitTests.Domain.Entities;

public sealed class BookTests
{
    private const string ValidIsbn13 = "9780306406157";
    private const int ValidPublicationYear = 2020;

    [Fact]
    public void Create_WithValidData_NormalizesValuesAndCreatesBook()
    {
        var book = Book.Create(
            "  Clean Code  ",
            Author.Create("  Robert C. Martin  "),
            "978-0-306-40615-7",
            ValidPublicationYear,
            "  A practical book about writing code.  ");

        Assert.NotEqual(Guid.Empty, book.Id);
        Assert.Equal("Clean Code", book.Title);
        Assert.Equal("Robert C. Martin", book.Author.Name);
        Assert.Equal(book.Author.Id, book.AuthorId);
        Assert.Equal(ValidIsbn13, book.Isbn);
        Assert.Equal(ValidPublicationYear, book.PublicationYear);
        Assert.Equal("A practical book about writing code.", book.Description);
    }

    [Theory]
    [InlineData("0-306-40615-2", "0306406152")]
    [InlineData("0-8044-2957-x", "080442957X")]
    [InlineData("978-0-306-40615-7", "9780306406157")]
    public void Create_WithIsbn10OrIsbn13_ReturnsCanonicalIsbn(string isbn, string expectedIsbn)
    {
        var book = Book.Create("Title", Author.Create("Author"), isbn, ValidPublicationYear, null);

        Assert.Equal(expectedIsbn, book.Isbn);
    }

    [Fact]
    public void Create_WhenTitleIsMissing_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create(null, Author.Create("Author"), ValidIsbn13, ValidPublicationYear, null));

        Assert.Equal("Title is required.", exception.Message);
    }

    [Fact]
    public void Create_WhenAuthorIsMissing_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", null, ValidIsbn13, ValidPublicationYear, null));

        Assert.Equal("Author is required.", exception.Message);
    }

    [Fact]
    public void Create_WhenIsbnIsMissing_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), "", ValidPublicationYear, null));

        Assert.Equal("ISBN is required.", exception.Message);
    }

    [Fact]
    public void Create_WhenPublicationYearIsMissing_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), ValidIsbn13, null, null));

        Assert.Equal("Publication year is required.", exception.Message);
    }

    [Fact]
    public void Create_WhenIsbnIsTooShort_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), "123456789", ValidPublicationYear, null));

        Assert.Equal("ISBN must use the ISBN-10 or ISBN-13 format.", exception.Message);
    }

    [Fact]
    public void Create_WhenIsbnIsTooLong_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), "123456789012", ValidPublicationYear, null));

        Assert.Equal("ISBN must use the ISBN-10 or ISBN-13 format.", exception.Message);
    }

    [Fact]
    public void Create_WhenIsbn13ContainsNonDigit_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), "123456789012X", ValidPublicationYear, null));

        Assert.Equal("ISBN must use the ISBN-10 or ISBN-13 format.", exception.Message);
    }

    [Fact]
    public void Create_WhenPublicationYearIsBefore1450_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), ValidIsbn13, 1449, null));

        Assert.Equal(
            $"Publication year must be between 1450 and {DateTime.UtcNow.Year}.",
            exception.Message);
    }

    [Fact]
    public void Create_WhenPublicationYearIsInTheFuture_ThrowsDomainValidationException()
    {
        var futureYear = DateTime.UtcNow.Year + 1;

        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), ValidIsbn13, futureYear, null));

        Assert.Equal(
            $"Publication year must be between 1450 and {DateTime.UtcNow.Year}.",
            exception.Message);
    }

    [Fact]
    public void Create_WhenPublicationYearIs1450_CreatesBook()
    {
        var book = Book.Create("Title", Author.Create("Author"), ValidIsbn13, 1450, null);

        Assert.Equal(1450, book.PublicationYear);
    }

    [Fact]
    public void Create_WhenPublicationYearIsCurrentYear_CreatesBook()
    {
        var currentYear = DateTime.UtcNow.Year;

        var book = Book.Create("Title", Author.Create("Author"), ValidIsbn13, currentYear, null);

        Assert.Equal(currentYear, book.PublicationYear);
    }

    [Fact]
    public void Create_WhenTitleExceeds200Characters_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create(new string('T', 201), Author.Create("Author"), ValidIsbn13, ValidPublicationYear, null));

        Assert.Equal("Title cannot be longer than 200 characters.", exception.Message);
    }

    [Fact]
    public void Create_WhenDescriptionExceeds2000Characters_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            Book.Create("Title", Author.Create("Author"), ValidIsbn13, ValidPublicationYear, new string('D', 2001)));

        Assert.Equal("Description cannot be longer than 2000 characters.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankDescription_SetsDescriptionToNull(string? description)
    {
        var book = Book.Create("Title", Author.Create("Author"), ValidIsbn13, ValidPublicationYear, description);

        Assert.Null(book.Description);
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesPropertiesAndPreservesId()
    {
        var book = CreateValidBook();
        var originalId = book.Id;

        book.UpdateDetails(
            "  Refactoring  ",
            Author.Create("  Martin Fowler  "),
            "0-8044-2957-x",
            1999,
            "  Improving the design of existing code.  ");

        Assert.Equal(originalId, book.Id);
        Assert.Equal("Refactoring", book.Title);
        Assert.Equal("Martin Fowler", book.Author.Name);
        Assert.Equal(book.Author.Id, book.AuthorId);
        Assert.Equal("080442957X", book.Isbn);
        Assert.Equal(1999, book.PublicationYear);
        Assert.Equal("Improving the design of existing code.", book.Description);
    }

    [Fact]
    public void UpdateDetails_WithInvalidData_ThrowsAndDoesNotPartiallyUpdateBook()
    {
        var book = CreateValidBook();
        var originalId = book.Id;
        var originalTitle = book.Title;
        var originalAuthor = book.Author;
        var originalIsbn = book.Isbn;
        var originalPublicationYear = book.PublicationYear;
        var originalDescription = book.Description;

        var exception = Assert.Throws<DomainValidationException>(() =>
            book.UpdateDetails("Updated title", null, "080442957X", 1999, "Updated description"));

        Assert.Equal("Author is required.", exception.Message);
        Assert.Equal(originalId, book.Id);
        Assert.Equal(originalTitle, book.Title);
        Assert.Equal(originalAuthor, book.Author);
        Assert.Equal(originalIsbn, book.Isbn);
        Assert.Equal(originalPublicationYear, book.PublicationYear);
        Assert.Equal(originalDescription, book.Description);
    }

    [Fact]
    public void NewBook_IsAvailable()
    {
        Assert.True(CreateValidBook().IsAvailable);
    }

    [Fact]
    public void MarkBorrowed_MakesBookUnavailable()
    {
        var book = CreateValidBook();
        book.MarkBorrowed();
        Assert.False(book.IsAvailable);
        Assert.Throws<DomainValidationException>(() => book.UpdateDetails(null, book.Author, book.Isbn, 2020, null));
        Assert.False(book.IsAvailable);
    }

    [Fact]
    public void MarkBorrowed_Twice_ThrowsConflict()
    {
        var book = CreateValidBook();
        book.MarkBorrowed();
        Assert.Throws<DomainConflictException>(() => book.MarkBorrowed());
        Assert.False(book.IsAvailable);
    }

    [Fact]
    public void MarkReturned_AllowsAnotherBorrow()
    {
        var book = CreateValidBook();
        book.MarkBorrowed();
        book.MarkReturned();
        Assert.True(book.IsAvailable);
        book.MarkBorrowed();
        Assert.False(book.IsAvailable);
    }

    [Fact]
    public void MarkReturned_WhenAvailable_ThrowsConflict()
    {
        var book = CreateValidBook();
        Assert.Throws<DomainConflictException>(() => book.MarkReturned());
        Assert.True(book.IsAvailable);
    }

    [Fact]
    public void UpdateDetails_DoesNotChangeBorrowedState()
    {
        var book = CreateValidBook();
        book.MarkBorrowed();
        book.UpdateDetails("Updated", book.Author, book.Isbn, 2020, null);
        Assert.False(book.IsAvailable);
    }

    private static Book CreateValidBook()
    {
        return Book.Create(
            "Original title",
            Author.Create("Original author"),
            ValidIsbn13,
            ValidPublicationYear,
            "Original description");
    }
}