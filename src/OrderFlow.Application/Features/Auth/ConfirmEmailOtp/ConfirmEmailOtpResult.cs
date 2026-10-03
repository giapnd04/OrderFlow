namespace OrderFlow.Application.Features.Auth.ConfirmEmailOtp;

/// <summary>
/// If <paramref name="IsCustomerLinked"/> just became true, the caller must log in again:
/// tokens issued earlier do not carry the customer link.
/// </summary>
public sealed record ConfirmEmailOtpResult(int UserId, bool IsEmailVerified, bool IsCustomerLinked);
