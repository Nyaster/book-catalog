using BookCatalog.Api.Contracts.Loans;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using BookCatalog.Application.Loans.Contracts;
using BookCatalog.Application.Loans.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class LoansController(ILendingService lendingService) : ControllerBase
{
    private readonly ILendingService _lendingService =
        lendingService ?? throw new ArgumentNullException(nameof(lendingService));

    [HttpPost("books/{bookId:guid}/loans")]
    [EndpointSummary("Borrow an available book")]
    [EndpointDescription("Creates a loan for the supplied user. One book represents one copy; an active loan prevents another borrow.")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Borrow(Guid bookId, LendingRequest request, CancellationToken cancellationToken)
    {
        var loan = await _lendingService.BorrowAsync(bookId, request.UserId ?? Guid.Empty, cancellationToken);
        var response = MapToResponse(loan);
        return CreatedAtAction(nameof(GetById), new { loanId = response.Id }, response);
    }

    [HttpPost("loans/{loanId:guid}/return")]
    [EndpointSummary("Return a borrowed book")]
    [EndpointDescription("Completes this loan for its borrower. Repeating a return or supplying a different user produces 409.")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> Return(Guid loanId, LendingRequest request, CancellationToken cancellationToken)
    {
        var loan = await _lendingService.ReturnAsync(loanId, request.UserId ?? Guid.Empty, cancellationToken);
        return Ok(MapToResponse(loan));
    }

    [HttpGet("loans/{loanId:guid}")]
    [EndpointSummary("Get a loan by ID")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanResponse>> GetById(Guid loanId, CancellationToken cancellationToken) =>
        Ok(MapToResponse(await _lendingService.GetByIdAsync(loanId, cancellationToken)));

    [HttpGet("books/{bookId:guid}/loans")]
    [EndpointSummary("Get a book's borrowing history")]
    [EndpointDescription("Includes active and returned loans, newest borrowing first, with loan ID as the tie-breaker.")]
    [ProducesResponseType<PagedResult<LoanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetByBook(
        Guid bookId, [FromQuery] GetLoansRequest request, CancellationToken cancellationToken) =>
        Ok(MapPage(await _lendingService.GetByBookAsync(bookId, new PageQuery(request.Page, request.PageSize), cancellationToken)));

    [HttpGet("users/{userId:guid}/loans")]
    [EndpointSummary("Get a user's borrowing history")]
    [EndpointDescription("Includes active and returned loans, newest borrowing first, with loan ID as the tie-breaker.")]
    [ProducesResponseType<PagedResult<LoanResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> GetByUser(
        Guid userId, [FromQuery] GetLoansRequest request, CancellationToken cancellationToken) =>
        Ok(MapPage(await _lendingService.GetByUserAsync(userId, new PageQuery(request.Page, request.PageSize), cancellationToken)));

    private static LoanResponse MapToResponse(LoanDto loan) => new(
        loan.Id, loan.BookId, loan.BookTitle, loan.UserId, loan.UserDisplayName, loan.BorrowedAt, loan.ReturnedAt);

    private static PagedResult<LoanResponse> MapPage(PagedResult<LoanDto> page) => new(
        page.Items.Select(MapToResponse).ToArray(), page.Page, page.PageSize, page.TotalCount);
}
