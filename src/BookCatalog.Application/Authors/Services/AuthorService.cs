using BookCatalog.Application.Authors.Contracts;
using BookCatalog.Application.Authors.Exceptions;
using BookCatalog.Application.Authors.Persistence;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BookCatalog.Application.Authors.Services;

public sealed class AuthorService(IAuthorRepository authorRepository, ILogger<AuthorService> logger) : IAuthorService
{
    private readonly IAuthorRepository _authorRepository =
        authorRepository ?? throw new ArgumentNullException(nameof(authorRepository));
    private readonly ILogger<AuthorService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<AuthorDto> CreateAsync(CreateAuthorCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var author = Author.Create(command.Name);
        await _authorRepository.AddAsync(author, cancellationToken);
        _logger.LogInformation("Created author {AuthorId}.", author.Id);
        return MapToDto(author);
    }

    public async Task<AuthorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var author = await _authorRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AuthorNotFoundException(id);
        return MapToDto(author);
    }

    public async Task<PagedResult<AuthorDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var authors = await _authorRepository.GetPageAsync(query, cancellationToken);
        return new PagedResult<AuthorDto>(authors.Items.Select(MapToDto).ToArray(),
            authors.Page, authors.PageSize, authors.TotalCount);
    }

    private static AuthorDto MapToDto(Author author) => new(author.Id, author.Name);
}
