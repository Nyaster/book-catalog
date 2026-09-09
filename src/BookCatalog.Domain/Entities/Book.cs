using BookCatalog.Domain.Exceptions;
using BookCatalog.Domain.ValueObjects;

namespace BookCatalog.Domain.Entities;

public sealed class Book
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public bool IsAvailable { get; private set; } = true;
    public Guid AuthorId { get; private set; }
    public Author Author { get; private set; } = null!;
    public string Isbn { get; private set; }
    public int PublicationYear { get; private set; }
    public string? Description { get; private set; }


    private Book()
    {
        Title = null!;
        Isbn = null!;
    }

    private Book(
        Guid id,
        string title,
        Author author,
        string isbn,
        int publicationYear,
        string? description)
    {
        Id = id;
        Title = title;
        Author = author;
        AuthorId = author.Id;
        Isbn = isbn;
        PublicationYear = publicationYear;
        Description = description;
    }

    public static Book Create(
        string? title,
        Author? author,
        string? isbn,
        int? publicationYear,
        string? description)
    {
        var normalizedTitle = NormalizeRequired(title, "Title", 200);
        var validAuthor = author ?? throw new DomainValidationException("Author is required.");
        var normalizedIsbn = NormalizeIsbn(isbn);
        var validPublicationYear = ValidatePublicationYear(publicationYear);
        var normalizedDescription = NormalizeDescription(description);

        return new Book(
            Guid.NewGuid(),
            normalizedTitle,
            validAuthor,
            normalizedIsbn,
            validPublicationYear,
            normalizedDescription);
    }

    public void UpdateDetails(
        string? title,
        Author? author,
        string? isbn,
        int? publicationYear,
        string? description)
    {
        var normalizedTitle = NormalizeRequired(title, "Title", 200);
        var validAuthor = author ?? throw new DomainValidationException("Author is required.");
        var normalizedIsbn = NormalizeIsbn(isbn);
        var validPublicationYear = ValidatePublicationYear(publicationYear);
        var normalizedDescription = NormalizeDescription(description);

        Title = normalizedTitle;
        Author = validAuthor;
        AuthorId = validAuthor.Id;
        Isbn = normalizedIsbn;
        PublicationYear = validPublicationYear;
        Description = normalizedDescription;
    }

    public void MarkBorrowed()
    {
        if (!IsAvailable) throw new DomainConflictException("This book is already borrowed.");
        IsAvailable = false;
    }

    public void MarkReturned()
    {
        if (IsAvailable) throw new DomainConflictException("This book is already available.");
        IsAvailable = true;
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
        return IsbnNormalizer.NormalizeRequired(value);
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