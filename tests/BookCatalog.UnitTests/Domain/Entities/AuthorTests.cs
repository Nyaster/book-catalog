using BookCatalog.Domain.Entities;
using BookCatalog.Domain.Exceptions;

namespace BookCatalog.UnitTests.Domain.Entities;

public sealed class AuthorTests
{
    [Fact]
    public void Create_NormalizesNameAndAssignsId()
    {
        var author = Author.Create("  Robert C. Martin  ");
        Assert.Equal("Robert C. Martin", author.Name);
        Assert.NotEqual(Guid.Empty, author.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameIsMissing_Throws(string? name)
    {
        var error = Assert.Throws<DomainValidationException>(() => Author.Create(name));
        Assert.Equal("Author is required.", error.Message);
    }

    [Fact]
    public void Create_WhenNameExceeds150Characters_Throws()
    {
        var error = Assert.Throws<DomainValidationException>(() => Author.Create(new string('A', 151)));
        Assert.Equal("Author cannot be longer than 150 characters.", error.Message);
    }

    [Fact]
    public void Create_Allows150CharactersAfterTrimming()
    {
        var author = Author.Create($" {new string('A', 150)} ");
        Assert.Equal(150, author.Name.Length);
    }

    [Fact]
    public void SameName_DoesNotMeanSameAuthor()
    {
        var first = Author.Create("Alex Smith");
        var second = Author.Create("Alex Smith");
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void MultipleBooks_CanReferenceTheSameAuthor()
    {
        var author = Author.Create("Robert C. Martin");
        var first = Book.Create("First", author, "9780306406157", 2020, null);
        var second = Book.Create("Second", author, "080442957X", 2020, null);
        Assert.Equal(first.AuthorId, second.AuthorId);
        Assert.Same(author, first.Author);
        Assert.Same(author, second.Author);
    }
}
