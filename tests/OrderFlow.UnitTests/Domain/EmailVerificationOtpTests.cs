using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Domain;

public sealed class EmailVerificationOtpTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ExpiresTenMinutesAfterNow()
    {
        var otp = EmailVerificationOtp.Create(1, "123456", Now);

        Assert.Equal(Now.AddMinutes(10), otp.ExpiresAt);
        Assert.Null(otp.ConsumedAt);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("")]
    [InlineData(null)]
    public void Create_MalformedCode_Throws(string? code)
    {
        Assert.Throws<DomainException>(() => EmailVerificationOtp.Create(1, code!, Now));
    }

    [Fact]
    public void Create_InvalidUserId_Throws()
    {
        Assert.Throws<DomainException>(() => EmailVerificationOtp.Create(0, "123456", Now));
    }

    [Fact]
    public void IsUsable_BeforeExpiry_True()
    {
        var otp = EmailVerificationOtp.Create(1, "123456", Now);

        Assert.True(otp.IsUsable(Now.AddMinutes(9)));
    }

    [Fact]
    public void IsUsable_AtAndAfterExpiry_False()
    {
        var otp = EmailVerificationOtp.Create(1, "123456", Now);

        Assert.False(otp.IsUsable(Now.AddMinutes(10)));
        Assert.False(otp.IsUsable(Now.AddMinutes(11)));
    }

    [Fact]
    public void Consume_MakesItUnusableAndStopsReplay()
    {
        var otp = EmailVerificationOtp.Create(1, "123456", Now);

        otp.Consume(Now.AddMinutes(1));

        Assert.False(otp.IsUsable(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), otp.ConsumedAt);
        Assert.Throws<DomainException>(() => otp.Consume(Now.AddMinutes(2)));
    }

    [Fact]
    public void Consume_Expired_Throws()
    {
        var otp = EmailVerificationOtp.Create(1, "123456", Now);

        Assert.Throws<DomainException>(() => otp.Consume(Now.AddMinutes(30)));
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("123457", false)]
    [InlineData("12345", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Matches_ComparesExactCode(string? candidate, bool expected)
    {
        var otp = EmailVerificationOtp.Create(1, "123456", Now);

        Assert.Equal(expected, otp.Matches(candidate));
    }
}
