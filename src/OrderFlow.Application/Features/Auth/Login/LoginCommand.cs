using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
