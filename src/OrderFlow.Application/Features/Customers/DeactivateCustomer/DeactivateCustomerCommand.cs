using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.DeactivateCustomer;

public sealed record DeactivateCustomerCommand(int CustomerId) : ICommand<DeactivateCustomerResult>;
