using OrderFlow.Application.Abstractions.Validation;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Auth.CreateStaffUser;

public sealed class CreateStaffUserCommandValidator : Validator<CreateStaffUserCommand>
{
    protected override void Check(CreateStaffUserCommand command, ErrorCollector errors)
    {
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

        errors.AddIf(
            !Enum.IsDefined(command.Role) || command.Role == UserRole.Customer,
            nameof(command.Role),
            "Role must be one of: Sales, Warehouse, Administrator.");
    }
}
