using BookCatalog.Domain.ValueObjects;

namespace BookCatalog.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; }

    private User(Guid id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public static User Create(string? displayName) =>
        new(Guid.NewGuid(), EntityValidation.RequiredName(displayName, "Display name"));
}