using System.ComponentModel.DataAnnotations;

namespace BookCatalog.Api.Contracts.Books;

public sealed class UpdateBookRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot be longer than 200 characters.")]
    public string? Title { get; init; }

    [Required(ErrorMessage = "Author is required.")]
    [StringLength(150, ErrorMessage = "Author cannot be longer than 150 characters.")]
    public string? Author { get; init; }

    [Required(ErrorMessage = "ISBN is required.")]
    public string? Isbn { get; init; }

    [Required(ErrorMessage = "Publication year is required.")]
    [Range(1450, int.MaxValue, ErrorMessage = "Publication year must be 1450 or later.")]
    public int? PublicationYear { get; init; }

    [StringLength(2000, ErrorMessage = "Description cannot be longer than 2000 characters.")]
    public string? Description { get; init; }
}
