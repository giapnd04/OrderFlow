namespace OrderFlow.Application.Features.Customers.GetCustomerById;

/// <summary>
/// Outcome of a successful <see cref="GetCustomerByIdQuery"/>. A flat contract the API
/// can return directly without exposing the <c>Customer</c> domain entity (SDS §11).
/// </summary>
public sealed record GetCustomerByIdResult(
    int CustomerId,
    string Name,
    string Email,
    string? Phone);
