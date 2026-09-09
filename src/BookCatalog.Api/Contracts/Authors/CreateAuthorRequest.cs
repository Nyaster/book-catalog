using System.ComponentModel.DataAnnotations;

namespace BookCatalog.Api.Contracts.Authors;

public sealed class CreateAuthorRequest
{
    [Required(ErrorMessage = "Author name is required.")]
    [StringLength(150, ErrorMessage = "Author name cannot be longer than 150 characters.")]
    public string? Name { get; init; }
}
