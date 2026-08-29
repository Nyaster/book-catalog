using BookCatalog.Domain.ValueObjects;

namespace BookCatalog.Application.Books.Contracts;

public sealed record BookFilter
{
    public static BookFilter Empty { get; } = new();

    public BookFilter(
        string? title = null,
        string? author = null,
        string? isbn = null,
        int? publicationYear = null,
        int? publicationYearBefore = null,
        int? publicationYearAfter = null)
    {
        Title = NormalizeText(title);
        Author = NormalizeText(author);
        Isbn = IsbnNormalizer.NormalizeForSearch(isbn);
        PublicationYear = publicationYear;
        PublicationYearBefore = publicationYearBefore;
        PublicationYearAfter = publicationYearAfter;
    }

    public string? Title { get; }

    public string? Author { get; }

    public string? Isbn { get; }

    public int? PublicationYear { get; }

    public int? PublicationYearBefore { get; }

    public int? PublicationYearAfter { get; }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
