using BookCatalog.Api.Contracts.Books;
using BookCatalog.Application.Books.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController(IBookService bookService) : ControllerBase
{
    private readonly IBookService _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));

    [HttpPost]
    [EndpointSummary("Create a book")]
    [EndpointDescription("Adds a new book to the catalog. Each book must have a unique ISBN.")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> Create(
        CreateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await _bookService.CreateAsync(request.ToCommand(), cancellationToken);
        var response = book.ToResponse();

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    [HttpGet]
    [EndpointSummary("Get all books")]
    [EndpointDescription("Returns all books ordered by title and then by author.")]
    [ProducesResponseType<IReadOnlyList<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var books = await _bookService.GetAllAsync(cancellationToken);
        var response = books.Select(book => book.ToResponse()).ToArray();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a book by ID")]
    [EndpointDescription("Returns the book with the specified ID.")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var book = await _bookService.GetByIdAsync(id, cancellationToken);

        return Ok(book.ToResponse());
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Update a book")]
    [EndpointDescription("Replaces the details of an existing book. The ISBN must remain unique.")]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> Update(
        Guid id,
        UpdateBookRequest request,
        CancellationToken cancellationToken)
    {
        var book = await _bookService.UpdateAsync(id, request.ToCommand(), cancellationToken);

        return Ok(book.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a book")]
    [EndpointDescription("Removes the book with the specified ID from the catalog.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _bookService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}