using BookCatalog.Api.Contracts.Books;
using BookCatalog.Application.Books.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Controllers;

[ApiController]
[Route("api/books")]
public sealed class BooksController(IBookService bookService, ILogger<BooksController> logger) : ControllerBase
{
    private readonly IBookService _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
    private readonly ILogger<BooksController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        _logger.LogInformation("Received request to create a book.");

        var book = await _bookService.CreateAsync(request.ToCommand(), cancellationToken);
        var response = book.ToResponse();

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    [HttpGet]
    [EndpointSummary("Get books")]
    [EndpointDescription("Returns a page of books ordered by title and then by author. Page numbering starts at 1.")]
    [ProducesResponseType<PagedBooksResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedBooksResponse>> GetPage(
        [FromQuery] GetBooksRequest request,
        CancellationToken cancellationToken)
    {
        var books = await _bookService.GetPageAsync(request.ToPageRequest(), cancellationToken);

        return Ok(books.ToResponse());
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
        _logger.LogInformation("Received request to update book {BookId}.", id);

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
        _logger.LogInformation("Received request to delete book {BookId}.", id);

        await _bookService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}