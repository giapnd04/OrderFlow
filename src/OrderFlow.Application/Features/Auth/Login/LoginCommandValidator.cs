using OrderFlow.Application.Abstractions.Validation;

namespace OrderFlow.Application.Features.Auth.Login;

/// <summary>
/// Shape only. The password policy is deliberately NOT applied here: a login must fail as
/// "wrong credentials", never as "your password does not meet the rules".
/// </summary>
public sealed class LoginCommandValidator : Validator<LoginCommand>
{
    protected override void Check(LoginCommand command, ErrorCollector errors)
    {
        errors.AddIf(string.IsNullOrWhiteSpace(command.Email), nameof(command.Email), "Email is required.");
        errors.AddIf(command.Email?.Length > 256, nameof(command.Email), "Email must be at most 256 characters.");
        errors.AddIf(string.IsNullOrEmpty(command.Password), nameof(command.Password), "Password is required.");
        errors.AddIf(command.Password?.Length > PasswordPolicy.MaxLength, nameof(command.Password), "Password is too long.");
    }
}
