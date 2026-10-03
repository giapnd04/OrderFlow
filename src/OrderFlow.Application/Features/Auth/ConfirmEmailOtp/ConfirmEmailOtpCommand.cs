using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Auth.ConfirmEmailOtp;

public sealed record ConfirmEmailOtpCommand(int UserId, string Code) : ICommand<ConfirmEmailOtpResult>;
