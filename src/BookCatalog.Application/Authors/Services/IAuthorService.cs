using BookCatalog.Application.Authors.Contracts;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;

namespace BookCatalog.Application.Authors.Services;

public interface IAuthorService
{
    Task<AuthorDto> CreateAsync(CreateAuthorCommand command, CancellationToken cancellationToken = default);
    Task<AuthorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AuthorDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken = default);
}
