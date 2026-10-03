using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Auth.Register;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Auth;

public sealed class RegisterCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeUserRepository _users = new();
    private readonly AuthFakeCustomerRepository _customers = new();
    private readonly FakeOtpRepository _otps = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly RecordingEmailSender _email = new();
    private readonly FakeTransactionRunner _transactions = new();

    private RegisterCommandHandler Handler() => new(
        _users, _customers, _otps, _hasher, new FixedOtpGenerator("424242"), _email, _transactions, new FixedTimeProvider(Now));

    [Fact]
    public async Task Handle_NewEmail_CreatesLinkedCustomerAndUnverifiedUser()
    {
        var result = await Handler().Handle(new RegisterCommand("Alice", "Alice@Example.com", "Password1", "0900"));

        var customer = Assert.Single(_customers.Customers);
        var user = Assert.Single(_users.Users);

        Assert.Equal("Alice", customer.Name);
        Assert.Equal("alice@example.com", customer.Email);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Equal(customer.Id, user.CustomerId);
        Assert.False(user.IsEmailVerified);
        Assert.True(result.IsCustomerLinked);
        Assert.False(result.IsEmailVerified);
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task Handle_StoresHashNeverThePlainPassword()
    {
        await Handler().Handle(new RegisterCommand("Alice", "a@example.com", "Password1", null));

        Assert.Equal("hashed:Password1", _users.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task Handle_CreatesOtpAndEmailsTheCode()
    {
        await Handler().Handle(new RegisterCommand("Alice", "a@example.com", "Password1", null));

        var otp = Assert.Single(_otps.Otps);
        Assert.Equal("424242", otp.Code);
        Assert.Equal(_users.Users.Single().Id, otp.UserId);
        Assert.Equal(Now.AddMinutes(10), otp.ExpiresAt);

        var sent = Assert.Single(_email.Sent);
        Assert.Equal("a@example.com", sent.To);
        Assert.Contains("424242", sent.Body);
    }

    [Fact]
    public async Task Handle_EmailMatchesExistingCustomer_CreatesUnlinkedUserAndNoDuplicateCustomer()
    {
        var existing = _customers.Seed("Walk-in Customer", "walkin@example.com");

        var result = await Handler().Handle(new RegisterCommand("Someone", "walkin@example.com", "Password1", null));

        Assert.Single(_customers.Customers);
        var user = Assert.Single(_users.Users);
        Assert.Null(user.CustomerId);
        Assert.False(result.IsCustomerLinked);
        Assert.NotEqual(existing.Id, user.CustomerId);
        Assert.Single(_otps.Otps);
        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task Handle_EmailAlreadyRegistered_ThrowsConflictCaseInsensitively()
    {
        await Handler().Handle(new RegisterCommand("Alice", "a@example.com", "Password1", null));

        await Assert.ThrowsAsync<ConflictException>(
            () => Handler().Handle(new RegisterCommand("Alice 2", "A@EXAMPLE.COM", "Password1", null)));

        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Handle_ExistingCustomerAlreadyLinkedToAnotherUser_ThrowsConflict()
    {
        var existing = _customers.Seed("Linked", "shared@example.com");
        var owner = User.Create("owner@example.com", "hash", UserRole.Customer);
        owner.LinkCustomer(existing.Id);
        await _users.AddAsync(owner);

        await Assert.ThrowsAsync<ConflictException>(
            () => Handler().Handle(new RegisterCommand("New", "shared@example.com", "Password1", null)));

        Assert.Single(_users.Users);
        Assert.Empty(_otps.Otps);
    }

    [Fact]
    public async Task Handle_EmailSendingFails_PropagatesAndTheTransactionSawTheFailure()
    {
        _email.FailWith = new InvalidOperationException("smtp down");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler().Handle(new RegisterCommand("Alice", "a@example.com", "Password1", null)));

        // The email is sent inside the unit of work, so a delivery failure aborts the whole signup.
        Assert.Equal(1, _transactions.Failures);
    }

    [Fact]
    public async Task Handle_InvalidCustomerName_DomainRejectsInsideTheTransaction()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => Handler().Handle(new RegisterCommand(" ", "a@example.com", "Password1", null)));

        Assert.Equal(1, _transactions.Failures);
        Assert.Empty(_users.Users);
    }
}
