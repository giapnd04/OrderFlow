using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Domain;

public sealed class UserTests
{
    [Fact]
    public void Create_NormalizesEmailToLowerCaseAndTrims()
    {
        var user = User.Create("  Alice@Example.COM ", "hash", UserRole.Customer);

        Assert.Equal("alice@example.com", user.Email);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.CustomerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData(null)]
    public void Create_InvalidEmail_Throws(string? email)
    {
        Assert.Throws<DomainException>(() => User.Create(email!, "hash", UserRole.Customer));
    }

    [Fact]
    public void Create_EmptyPasswordHash_Throws()
    {
        Assert.Throws<DomainException>(() => User.Create("a@example.com", " ", UserRole.Customer));
    }

    [Fact]
    public void Create_UndefinedRole_Throws()
    {
        Assert.Throws<DomainException>(() => User.Create("a@example.com", "hash", (UserRole)99));
    }

    [Fact]
    public void LinkCustomer_CustomerRole_SetsCustomerId()
    {
        var user = User.Create("a@example.com", "hash", UserRole.Customer);

        user.LinkCustomer(7);

        Assert.Equal(7, user.CustomerId);
    }

    [Fact]
    public void LinkCustomer_SameCustomerTwice_IsIdempotent()
    {
        var user = User.Create("a@example.com", "hash", UserRole.Customer);
        user.LinkCustomer(7);

        user.LinkCustomer(7);

        Assert.Equal(7, user.CustomerId);
    }

    [Fact]
    public void LinkCustomer_DifferentCustomer_Throws()
    {
        var user = User.Create("a@example.com", "hash", UserRole.Customer);
        user.LinkCustomer(7);

        Assert.Throws<DomainException>(() => user.LinkCustomer(8));
        Assert.Equal(7, user.CustomerId);
    }

    [Theory]
    [InlineData(UserRole.Sales)]
    [InlineData(UserRole.Warehouse)]
    [InlineData(UserRole.Administrator)]
    public void LinkCustomer_StaffRole_Throws(UserRole role)
    {
        var user = User.Create("staff@example.com", "hash", role);

        Assert.Throws<DomainException>(() => user.LinkCustomer(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LinkCustomer_NonPositiveId_Throws(int customerId)
    {
        var user = User.Create("a@example.com", "hash", UserRole.Customer);

        Assert.Throws<DomainException>(() => user.LinkCustomer(customerId));
    }

    [Fact]
    public void MarkEmailVerified_IsIdempotent()
    {
        var user = User.Create("a@example.com", "hash", UserRole.Customer);

        user.MarkEmailVerified();
        user.MarkEmailVerified();

        Assert.True(user.IsEmailVerified);
    }
}
