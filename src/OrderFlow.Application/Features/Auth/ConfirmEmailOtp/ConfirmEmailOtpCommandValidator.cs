using OrderFlow.Application.Abstractions.Validation;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Auth.ConfirmEmailOtp;

public sealed class ConfirmEmailOtpCommandValidator : Validator<ConfirmEmailOtpCommand>
{
    protected override void Check(ConfirmEmailOtpCommand command, ErrorCollector errors)
    {
        errors.AddIf(command.UserId <= 0, nameof(command.UserId), "UserId must be greater than zero.");

        errors.AddIf(
            command.Code is null
                || command.Code.Length != EmailVerificationOtp.CodeLength
                || !command.Code.All(char.IsAsciiDigit),
            nameof(command.Code),
            $"Code must be exactly {EmailVerificationOtp.CodeLength} digits.");
    }
}
