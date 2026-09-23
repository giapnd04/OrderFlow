namespace OrderFlow.Application.Features.Customers.DeactivateCustomer;

public sealed record DeactivateCustomerResult(int CustomerId, string Status);
