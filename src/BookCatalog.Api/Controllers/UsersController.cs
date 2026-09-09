using BookCatalog.Api.Contracts.Users;
using BookCatalog.Application.Users.Contracts;
using BookCatalog.Application.Users.Services;
using BookCatalog.Application.Books.Contracts;
using BookCatalog.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    private readonly IUserService _userService =
        userService ?? throw new ArgumentNullException(nameof(userService));

    [HttpPost]
    [EndpointSummary("Create a user")]
    [EndpointDescription("Creates a borrower record with a display name. Different users may share a display name.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(new CreateUserCommand(request.DisplayName), cancellationToken);
        var response = new UserResponse(user.Id, user.DisplayName);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a user by ID")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(id, cancellationToken);
        return Ok(new UserResponse(user.Id, user.DisplayName));
    }

    [HttpGet]
    [EndpointSummary("Get a page of users")]
    [EndpointDescription("Returns users ordered by display name and then ID. Page numbering starts at 1; maximum page size is 100.")]
    [ProducesResponseType<PagedResult<UserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<UserResponse>>> GetPage(
        [FromQuery] GetUsersRequest request, CancellationToken cancellationToken)
    {
        var users = await _userService.GetPageAsync(new PageQuery(request.Page, request.PageSize), cancellationToken);
        return Ok(new PagedResult<UserResponse>(
            users.Items.Select(user => new UserResponse(user.Id, user.DisplayName)).ToArray(),
            users.Page, users.PageSize, users.TotalCount));
    }
}
