namespace OrderFlow.Application.Abstractions.Security;

public interface IOtpGenerator
{
    /// <summary>A cryptographically random numeric code, <c>EmailVerificationOtp.CodeLength</c> digits long.</summary>
    string GenerateCode();
}
