using BookCatalog.Domain.ValueObjects;

namespace BookCatalog.Domain.Entities;

public sealed class Author
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    private Author(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public static Author Create(string? name) =>
        new(Guid.NewGuid(), EntityValidation.RequiredName(name, "Author"));
}