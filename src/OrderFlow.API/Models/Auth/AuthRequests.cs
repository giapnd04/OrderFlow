using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Models.Auth;

public sealed record RegisterRequest(string Name, string Email, string Password, string? Phone);

public sealed record LoginRequest(string Email, string Password);

public sealed record ConfirmEmailRequest(int UserId, string Code);

public sealed record CreateStaffUserRequest(string Email, string Password, UserRole Role);
