namespace OrderFlow.API.Models.Customers;

public sealed record CreateCustomerRequest(string Name, string Email, string? Phone);
