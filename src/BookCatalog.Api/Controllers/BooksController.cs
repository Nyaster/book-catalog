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
    [ProducesResponseType<IReadOnlyList<BookResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var books = await _bookService.GetAllAsync(cancellationToken);
        var response = books.Select(book => book.ToResponse()).ToArray();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _bookService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
