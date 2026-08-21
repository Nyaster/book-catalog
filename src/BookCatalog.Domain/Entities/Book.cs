namespace BookCatalog.Domain.Entities;

public class Book
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public string ISBN { get; set; }
    public DateTime PublishDate { get; set; }
    public string? Description { get; set; }

    private Book(Guid id, string title, string author, string isbn, DateTime publishDate, string? description)
    {
        Id = id;
        Title = title;
        Author = author;
        ISBN = isbn;
        PublishDate = publishDate;
        Description = description;
    }

    public static Book Create(string? title, string? author, string? isbn, DateTime? publishDate,
        string? description)
    {
        return new Book(Guid.NewGuid(), title, author, isbn, publishDate ?? DateTime.Now, description)
    }
    
}