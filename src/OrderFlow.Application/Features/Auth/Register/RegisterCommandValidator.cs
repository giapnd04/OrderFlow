using OrderFlow.Application.Abstractions.Validation;
using OrderFlow.Domain.Common;

namespace OrderFlow.Application.Features.Auth.Register;

public sealed class RegisterCommandValidator : Validator<RegisterCommand>
{
    protected override void Check(RegisterCommand command, ErrorCollector errors)
    {
        errors.AddIf(string.IsNullOrWhiteSpace(command.Name), nameof(command.Name), "Name is required.");
        errors.AddIf(command.Name?.Trim().Length > 200, nameof(command.Name), "Name must be at most 200 characters.");

        if (string.IsNullOrWhiteSpace(command.Email))
        {
            errors.Add(nameof(command.Email), "Email is required.");
        }
        else
        {
            errors.AddIf(!EmailRules.IsValid(command.Email), nameof(command.Email), "Email is not valid.");
            errors.AddIf(command.Email.Trim().Length > 256, nameof(command.Email), "Email must be at most 256 characters.");
        }

        PasswordPolicy.Check(command.Password, nameof(command.Password), errors);

        errors.AddIf(command.Phone?.Trim().Length > 32, nameof(command.Phone), "Phone must be at most 32 characters.");
    }
}
