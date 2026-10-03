using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Auth.CreateStaffUser;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Auth;

public sealed class CreateStaffUserCommandHandlerTests
{
    private readonly FakeUserRepository _users = new();

    private CreateStaffUserCommandHandler Handler() => new(_users, new FakePasswordHasher());

    [Theory]
    [InlineData(UserRole.Sales)]
    [InlineData(UserRole.Warehouse)]
    [InlineData(UserRole.Administrator)]
    public async Task Handle_CreatesVerifiedUnlinkedStaffAccount(UserRole role)
    {
        var result = await Handler().Handle(new CreateStaffUserCommand("Staff@Example.com", "Password1", role));

        var user = Assert.Single(_users.Users);
        Assert.Equal("staff@example.com", user.Email);
        Assert.Equal(role, user.Role);
        Assert.True(user.IsEmailVerified);
        Assert.Null(user.CustomerId);
        Assert.Equal("hashed:Password1", user.PasswordHash);
        Assert.Equal(role.ToString(), result.Role);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        await Handler().Handle(new CreateStaffUserCommand("a@example.com", "Password1", UserRole.Sales));

        await Assert.ThrowsAsync<ConflictException>(
            () => Handler().Handle(new CreateStaffUserCommand("A@example.com", "Password1", UserRole.Warehouse)));
    }
}
