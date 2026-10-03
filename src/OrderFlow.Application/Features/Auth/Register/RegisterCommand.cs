using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Auth.Register;

public sealed record RegisterCommand(string Name, string Email, string Password, string? Phone)
    : ICommand<RegisterResult>;
