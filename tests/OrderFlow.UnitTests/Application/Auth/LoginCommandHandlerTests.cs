using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Auth.Login;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Auth;

public sealed class LoginCommandHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeJwtTokenGenerator _tokens = new();

    private LoginCommandHandler Handler() => new(_users, _hasher, _tokens);

    private async Task<User> SeedUserAsync(
        string email = "a@example.com", string password = "Password1", UserRole role = UserRole.Customer, int? customerId = null)
    {
        var user = User.Create(email, "hashed:" + password, role);

        if (customerId is { } id)
        {
            user.LinkCustomer(id);
        }

        await _users.AddAsync(user);

        return user;
    }

    [Fact]
    public async Task Handle_CorrectCredentials_ReturnsBearerTokenAndProfile()
    {
        var user = await SeedUserAsync(customerId: 9);

        var result = await Handler().Handle(new LoginCommand("a@example.com", "Password1"));

        Assert.Equal($"token-for-{user.Id}", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("Customer", result.Role);
        Assert.Equal(9, result.CustomerId);
        Assert.Equal(user.Id, result.UserId);
        Assert.False(result.IsEmailVerified);
    }

    [Fact]
    public async Task Handle_EmailLookupIsCaseAndWhitespaceInsensitive()
    {
        await SeedUserAsync();

        var result = await Handler().Handle(new LoginCommand("  A@Example.COM ", "Password1"));

        Assert.Equal("a@example.com", result.Email);
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsAuthenticationFailed()
    {
        await SeedUserAsync();

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => Handler().Handle(new LoginCommand("a@example.com", "WrongPass1")));

        Assert.Null(_tokens.LastUser);
    }

    [Fact]
    public async Task Handle_UnknownEmail_ThrowsTheSameExceptionAsAWrongPassword()
    {
        var unknown = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => Handler().Handle(new LoginCommand("ghost@example.com", "Password1")));

        await SeedUserAsync();
        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => Handler().Handle(new LoginCommand("a@example.com", "WrongPass1")));

        Assert.Equal(wrong.Message, unknown.Message);
    }

    [Fact]
    public async Task Handle_UnknownEmail_StillSpendsAHashToEqualizeTiming()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => Handler().Handle(new LoginCommand("ghost@example.com", "Password1")));

        Assert.Equal(1, _hasher.HashCalls);
    }

    [Fact]
    public async Task Handle_UnverifiedEmail_CanStillLogIn()
    {
        var user = await SeedUserAsync();
        Assert.False(user.IsEmailVerified);

        var result = await Handler().Handle(new LoginCommand("a@example.com", "Password1"));

        Assert.False(result.IsEmailVerified);
    }

    [Theory]
    [InlineData(UserRole.Sales)]
    [InlineData(UserRole.Warehouse)]
    [InlineData(UserRole.Administrator)]
    public async Task Handle_StaffLogin_CarriesRoleAndNoCustomerId(UserRole role)
    {
        await SeedUserAsync("staff@example.com", role: role);

        var result = await Handler().Handle(new LoginCommand("staff@example.com", "Password1"));

        Assert.Equal(role.ToString(), result.Role);
        Assert.Null(result.CustomerId);
    }
}
