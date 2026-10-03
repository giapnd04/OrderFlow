namespace OrderFlow.Application.Features.Auth.Login;

public sealed record LoginResult(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    int UserId,
    string Email,
    string Role,
    int? CustomerId,
    bool IsEmailVerified);
