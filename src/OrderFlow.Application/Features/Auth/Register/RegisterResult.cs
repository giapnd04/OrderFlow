namespace OrderFlow.Application.Features.Auth.Register;

/// <summary>
/// <paramref name="IsCustomerLinked"/> is false on the claim path (an existing Sales-entered
/// customer shares this email): the link is only made once the emailed code is confirmed.
/// </summary>
public sealed record RegisterResult(int UserId, string Email, bool IsEmailVerified, bool IsCustomerLinked);
