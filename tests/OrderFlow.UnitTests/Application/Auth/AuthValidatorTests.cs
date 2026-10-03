using OrderFlow.Application.Features.Auth.ConfirmEmailOtp;
using OrderFlow.Application.Features.Auth.CreateStaffUser;
using OrderFlow.Application.Features.Auth.Login;
using OrderFlow.Application.Features.Auth.Register;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Auth;

public sealed class AuthValidatorTests
{
    private static readonly RegisterCommandValidator Register = new();

    private static RegisterCommand ValidRegister() => new("Alice", "alice@example.com", "Password1", "0900000000");

    [Fact]
    public void Register_ValidCommand_HasNoFailures()
    {
        Assert.Empty(Register.Validate(ValidRegister()));
    }

    [Theory]
    [InlineData("short1", "at least 8")]
    [InlineData("alllettersonly", "digit")]
    [InlineData("1234567890", "letter")]
    [InlineData("", "required")]
    public void Register_WeakPassword_IsRejectedWithAReason(string password, string expectedFragment)
    {
        var failures = Register.Validate(ValidRegister() with { Password = password });

        Assert.Contains(failures, f => f.Property == "Password" && f.Message.Contains(expectedFragment));
    }

    [Fact]
    public void Register_PasswordOverMaxLength_IsRejected()
    {
        var failures = Register.Validate(ValidRegister() with { Password = "a1" + new string('x', 200) });

        Assert.Contains(failures, f => f.Property == "Password" && f.Message.Contains("at most"));
    }

    [Fact]
    public void Register_ReportsEveryProblemAtOnce()
    {
        var failures = Register.Validate(new RegisterCommand("", "nope", "x", new string('9', 40)));

        var properties = failures.Select(f => f.Property).Distinct().ToList();
        Assert.Contains("Name", properties);
        Assert.Contains("Email", properties);
        Assert.Contains("Password", properties);
        Assert.Contains("Phone", properties);
    }

    [Fact]
    public void Login_DoesNotApplyThePasswordPolicy()
    {
        var failures = new LoginCommandValidator().Validate(new LoginCommand("a@example.com", "x"));

        Assert.Empty(failures);
    }

    [Fact]
    public void Login_RequiresEmailAndPassword()
    {
        var failures = new LoginCommandValidator().Validate(new LoginCommand("", ""));

        Assert.Contains(failures, f => f.Property == "Email");
        Assert.Contains(failures, f => f.Property == "Password");
    }

    [Theory]
    [InlineData(0, "123456", false)]
    [InlineData(1, "123456", true)]
    [InlineData(1, "12345", false)]
    [InlineData(1, "12345a", false)]
    [InlineData(1, "1234567", false)]
    public void ConfirmEmailOtp_ValidatesIdAndSixDigitCode(int userId, string code, bool valid)
    {
        var failures = new ConfirmEmailOtpCommandValidator().Validate(new ConfirmEmailOtpCommand(userId, code));

        Assert.Equal(valid, failures.Count == 0);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData((UserRole)42)]
    public void CreateStaffUser_RejectsCustomerAndUndefinedRoles(UserRole role)
    {
        var failures = new CreateStaffUserCommandValidator()
            .Validate(new CreateStaffUserCommand("s@example.com", "Password1", role));

        Assert.Contains(failures, f => f.Property == "Role");
    }

    [Fact]
    public void CreateStaffUser_ValidCommand_HasNoFailures()
    {
        var failures = new CreateStaffUserCommandValidator()
            .Validate(new CreateStaffUserCommand("s@example.com", "Password1", UserRole.Sales));

        Assert.Empty(failures);
    }
}
