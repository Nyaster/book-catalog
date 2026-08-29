using BookCatalog.Domain.Exceptions;

namespace BookCatalog.Domain.ValueObjects;

public static class IsbnNormalizer
{
    public static string NormalizeRequired(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("ISBN is required.");
        }

        var normalized = Normalize(value);

        if (!IsValid(normalized))
        {
            throw new DomainValidationException(
                "ISBN must use the ISBN-10 or ISBN-13 format.");
        }

        return normalized;
    }

    public static string? NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Normalize(value);

        return normalized.Length == 0 ? null : normalized;
    }

    private static string Normalize(string value)
    {
        return new string(
                value.Where(character => character != '-' && !char.IsWhiteSpace(character))
                    .ToArray())
            .ToUpperInvariant();
    }

    private static bool IsValid(string value)
    {
        var isIsbn10 =
            value.Length == 10 &&
            value[..9].All(char.IsDigit) &&
            (char.IsDigit(value[9]) || value[9] == 'X');

        var isIsbn13 =
            value.Length == 13 &&
            value.All(char.IsDigit);

        return isIsbn10 || isIsbn13;
    }
}
