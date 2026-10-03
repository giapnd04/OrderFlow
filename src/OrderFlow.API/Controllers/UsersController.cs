using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.API.Authorization;
using OrderFlow.API.Models.Auth;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Auth.CreateStaffUser;

namespace OrderFlow.API.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly ICommandHandler<CreateStaffUserCommand, CreateStaffUserResult> _createStaffUser;

    public UsersController(ICommandHandler<CreateStaffUserCommand, CreateStaffUserResult> createStaffUser)
    {
        _createStaffUser = createStaffUser;
    }

    [HttpPost("staff")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(CreateStaffUserResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateStaffUserResult>> CreateStaff(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createStaffUser.Handle(
            new CreateStaffUserCommand(request.Email, request.Password, request.Role),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
