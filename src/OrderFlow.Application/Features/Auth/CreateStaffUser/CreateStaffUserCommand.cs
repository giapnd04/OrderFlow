using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Auth.CreateStaffUser;

public sealed record CreateStaffUserCommand(string Email, string Password, UserRole Role)
    : ICommand<CreateStaffUserResult>;
