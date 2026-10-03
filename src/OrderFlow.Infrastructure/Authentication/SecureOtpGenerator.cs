using System.Security.Cryptography;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Authentication;

/// <summary>Uniformly random zero-padded numeric code from the OS CSPRNG (never <c>System.Random</c>).</summary>
public sealed class SecureOtpGenerator : IOtpGenerator
{
    public string GenerateCode()
    {
        var upperBound = (int)Math.Pow(10, EmailVerificationOtp.CodeLength);

        return RandomNumberGenerator.GetInt32(0, upperBound).ToString($"D{EmailVerificationOtp.CodeLength}");
    }
}
