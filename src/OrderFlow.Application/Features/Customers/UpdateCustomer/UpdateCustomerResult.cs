namespace OrderFlow.Application.Features.Customers.UpdateCustomer;

public sealed record UpdateCustomerResult(int CustomerId, string Name, string Email, string? Phone);
