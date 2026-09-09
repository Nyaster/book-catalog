using BookCatalog.Api.Contracts.Authors;
using BookCatalog.Application.Authors.Contracts;
using BookCatalog.Application.Authors.Services;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Controllers;

[ApiController]
[Route("api/authors")]
public sealed class AuthorsController(IAuthorService authorService) : ControllerBase
{
    private readonly IAuthorService _authorService =
        authorService ?? throw new ArgumentNullException(nameof(authorService));

    [HttpPost]
    [EndpointSummary("Create an author")]
    [EndpointDescription("Creates an author to select when creating or updating a book. Different authors may share a name.")]
    [ProducesResponseType<AuthorResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthorResponse>> Create(CreateAuthorRequest request, CancellationToken cancellationToken)
    {
        var author = await _authorService.CreateAsync(new CreateAuthorCommand(request.Name), cancellationToken);
        var response = new AuthorResponse(author.Id, author.Name);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get an author by ID")]
    [ProducesResponseType<AuthorResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var author = await _authorService.GetByIdAsync(id, cancellationToken);
        return Ok(new AuthorResponse(author.Id, author.Name));
    }

    [HttpGet]
    [EndpointSummary("Get a page of authors")]
    [EndpointDescription("Returns authors ordered by name and then ID. Page numbering starts at 1; maximum page size is 100.")]
    [ProducesResponseType<PagedResult<AuthorResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AuthorResponse>>> GetPage(
        [FromQuery] GetAuthorsRequest request, CancellationToken cancellationToken)
    {
        var authors = await _authorService.GetPageAsync(new PageQuery(request.Page, request.PageSize), cancellationToken);
        return Ok(new PagedResult<AuthorResponse>(
            authors.Items.Select(author => new AuthorResponse(author.Id, author.Name)).ToArray(),
            authors.Page, authors.PageSize, authors.TotalCount));
    }
}
