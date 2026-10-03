using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrderFlow.API.Extensions;
using OrderFlow.API.Models.Auth;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Features.Auth.ConfirmEmailOtp;
using OrderFlow.Application.Features.Auth.Login;
using OrderFlow.Application.Features.Auth.Register;

namespace OrderFlow.API.Controllers;

/// <summary>
/// Public endpoints (the only ones besides health). They depend on <c>ICommandHandler&lt;,&gt;</c>
/// rather than concrete handlers so every request runs through the validation pipeline, and
/// share the strict per-IP rate limit.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController : ControllerBase
{
    private readonly ICommandHandler<RegisterCommand, RegisterResult> _register;
    private readonly ICommandHandler<LoginCommand, LoginResult> _login;
    private readonly ICommandHandler<ConfirmEmailOtpCommand, ConfirmEmailOtpResult> _confirmEmail;

    public AuthController(
        ICommandHandler<RegisterCommand, RegisterResult> register,
        ICommandHandler<LoginCommand, LoginResult> login,
        ICommandHandler<ConfirmEmailOtpCommand, ConfirmEmailOtpResult> confirmEmail)
    {
        _register = register;
        _login = login;
        _confirmEmail = confirmEmail;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterResult>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _register.Handle(
            new RegisterCommand(request.Name, request.Email, request.Password, request.Phone),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResult>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _login.Handle(new LoginCommand(request.Email, request.Password), cancellationToken);

        return Ok(result);
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType(typeof(ConfirmEmailOtpResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfirmEmailOtpResult>> ConfirmEmail(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _confirmEmail.Handle(
            new ConfirmEmailOtpCommand(request.UserId, request.Code),
            cancellationToken);

        return Ok(result);
    }
}
