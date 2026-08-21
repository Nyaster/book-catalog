using BookCatalog.Domain.Exceptions;

namespace BookCatalog.Domain.Entities;

public sealed class Book
{
    public Guid Id { get; }
    public string Title { get; private set; }
    public string Author { get; private set; }
    public string Isbn { get; private set; }
    public int PublicationYear { get; private set; }
    public string? Description { get; private set; }

    private Book(
        Guid id,
        string title,
        string author,
        string isbn,
        int publicationYear,
        string? description)
    {
        Id = id;
        Title = title;
        Author = author;
        Isbn = isbn;
        PublicationYear = publicationYear;
        Description = description;
    }

    public static Book Create(
        string? title,
        string? author,
        string? isbn,
        int? publicationYear,
        string? description)
    {
        var normalizedTitle = NormalizeRequired(title, "Title", 200);
        var normalizedAuthor = NormalizeRequired(author, "Author", 150);
        var normalizedIsbn = NormalizeIsbn(isbn);
        var validPublicationYear = ValidatePublicationYear(publicationYear);
        var normalizedDescription = NormalizeDescription(description);

        return new Book(
            Guid.NewGuid(),
            normalizedTitle,
            normalizedAuthor,
            normalizedIsbn,
            validPublicationYear,
            normalizedDescription);
    }

    public void UpdateDetails(
        string? title,
        string? author,
        string? isbn,
        int? publicationYear,
        string? description)
    {
        var normalizedTitle = NormalizeRequired(title, "Title", 200);
        var normalizedAuthor = NormalizeRequired(author, "Author", 150);
        var normalizedIsbn = NormalizeIsbn(isbn);
        var validPublicationYear = ValidatePublicationYear(publicationYear);
        var normalizedDescription = NormalizeDescription(description);

        Title = normalizedTitle;
        Author = normalizedAuthor;
        Isbn = normalizedIsbn;
        PublicationYear = validPublicationYear;
        Description = normalizedDescription;
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{fieldName} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException(
                $"{fieldName} cannot be longer than {maxLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeIsbn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("ISBN is required.");
        }

        var normalized = new string(
                value.Where(character => character != '-' && !char.IsWhiteSpace(character))
                    .ToArray())
            .ToUpperInvariant();

        var isIsbn10 =
            normalized.Length == 10 &&
            normalized[..9].All(char.IsDigit) &&
            (char.IsDigit(normalized[9]) || normalized[9] == 'X');

        var isIsbn13 =
            normalized.Length == 13 &&
            normalized.All(char.IsDigit);

        if (!isIsbn10 && !isIsbn13)
        {
            throw new DomainValidationException(
                "ISBN must use the ISBN-10 or ISBN-13 format.");
        }

        return normalized;
    }

    private static int ValidatePublicationYear(int? publicationYear)
    {
        if (publicationYear is null)
        {
            throw new DomainValidationException("Publication year is required.");
        }

        const int minimumPublicationYear = 1450;
        var currentYear = DateTime.UtcNow.Year;

        if (publicationYear.Value < minimumPublicationYear || publicationYear.Value > currentYear)
        {
            throw new DomainValidationException(
                $"Publication year must be between {minimumPublicationYear} and {currentYear}.");
        }

        return publicationYear.Value;
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > 2000)
        {
            throw new DomainValidationException(
                "Description cannot be longer than 2000 characters.");
        }

        return normalized;
    }
}