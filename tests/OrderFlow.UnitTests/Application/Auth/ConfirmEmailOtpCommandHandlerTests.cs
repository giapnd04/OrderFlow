using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Auth.ConfirmEmailOtp;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Auth;

public sealed class ConfirmEmailOtpCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeUserRepository _users = new();
    private readonly AuthFakeCustomerRepository _customers = new();
    private readonly FakeOtpRepository _otps = new();
    private readonly FakeTransactionRunner _transactions = new();
    private readonly FixedTimeProvider _clock = new(Now);

    private ConfirmEmailOtpCommandHandler Handler() => new(_users, _customers, _otps, _transactions, _clock);

    private async Task<User> UserWithOtpAsync(string email = "a@example.com", string code = "123456", int? linkedCustomerId = null)
    {
        var user = User.Create(email, "hash", UserRole.Customer);

        if (linkedCustomerId is { } customerId)
        {
            user.LinkCustomer(customerId);
        }

        await _users.AddAsync(user);
        await _otps.AddAsync(EmailVerificationOtp.Create(user.Id, code, Now));

        return user;
    }

    [Fact]
    public async Task Handle_CorrectCode_VerifiesEmailAndConsumesOtp()
    {
        var user = await UserWithOtpAsync(linkedCustomerId: 5);

        var result = await Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456"));

        Assert.True(result.IsEmailVerified);
        Assert.True(user.IsEmailVerified);
        Assert.NotNull(_otps.Otps.Single().ConsumedAt);
    }

    [Fact]
    public async Task Handle_ClaimPath_LinksTheExistingCustomerWithTheSameEmail()
    {
        var customer = _customers.Seed("Walk-in", "walkin@example.com");
        var user = await UserWithOtpAsync("walkin@example.com");

        var result = await Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456"));

        Assert.True(result.IsCustomerLinked);
        Assert.Equal(customer.Id, user.CustomerId);
    }

    [Fact]
    public async Task Handle_ClaimedCustomerNowLinkedToSomeoneElse_VerifiesButDoesNotLink()
    {
        var customer = _customers.Seed("Walk-in", "walkin@example.com");
        var thief = User.Create("other@example.com", "hash", UserRole.Customer);
        thief.LinkCustomer(customer.Id);
        await _users.AddAsync(thief);
        var user = await UserWithOtpAsync("walkin@example.com");

        var result = await Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456"));

        Assert.True(result.IsEmailVerified);
        Assert.False(result.IsCustomerLinked);
        Assert.Null(user.CustomerId);
    }

    [Fact]
    public async Task Handle_NoMatchingCustomer_VerifiesWithoutLinking()
    {
        var user = await UserWithOtpAsync("nobody@example.com");

        var result = await Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456"));

        Assert.True(result.IsEmailVerified);
        Assert.False(result.IsCustomerLinked);
    }

    [Fact]
    public async Task Handle_WrongCode_ThrowsGenericValidationAndChangesNothing()
    {
        var user = await UserWithOtpAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "000000")));

        Assert.Equal("Invalid or expired verification code.", ex.Message);
        Assert.False(user.IsEmailVerified);
        Assert.Null(_otps.Otps.Single().ConsumedAt);
    }

    [Fact]
    public async Task Handle_ExpiredCode_ThrowsSameGenericError()
    {
        var user = await UserWithOtpAsync();
        _clock.Advance(TimeSpan.FromMinutes(11));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456")));

        Assert.Equal("Invalid or expired verification code.", ex.Message);
    }

    [Fact]
    public async Task Handle_ReplayingAConsumedCode_ThrowsSameGenericError()
    {
        var user = await UserWithOtpAsync(linkedCustomerId: 5);
        await Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "123456")));

        Assert.Equal("Invalid or expired verification code.", ex.Message);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsSameGenericErrorAsAWrongCode()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => Handler().Handle(new ConfirmEmailOtpCommand(999, "123456")));

        Assert.Equal("Invalid or expired verification code.", ex.Message);
    }

    [Fact]
    public async Task Handle_OnlyTheNewestCodeWorks()
    {
        var user = await UserWithOtpAsync(code: "111111", linkedCustomerId: 5);
        await _otps.AddAsync(EmailVerificationOtp.Create(user.Id, "222222", Now));

        await Assert.ThrowsAsync<ValidationException>(
            () => Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "111111")));

        var result = await Handler().Handle(new ConfirmEmailOtpCommand(user.Id, "222222"));
        Assert.True(result.IsEmailVerified);
    }
}
