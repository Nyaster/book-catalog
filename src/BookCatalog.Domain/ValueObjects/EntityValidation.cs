using BookCatalog.Domain.Exceptions;

namespace BookCatalog.Domain.ValueObjects;

public static class EntityValidation
{
    public static string RequiredName(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainValidationException($"{field} is required.");

        var name = value.Trim();
        if (name.Length > 150)
            throw new DomainValidationException($"{field} cannot be longer than 150 characters.");

        return name;
    }

    public static void RequireId(Guid id, string field)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException($"{field} must not be empty.");
    }
}