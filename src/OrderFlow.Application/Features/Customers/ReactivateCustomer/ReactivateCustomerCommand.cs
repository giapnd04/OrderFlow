using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.ReactivateCustomer;

public sealed record ReactivateCustomerCommand(int CustomerId) : ICommand<ReactivateCustomerResult>;
