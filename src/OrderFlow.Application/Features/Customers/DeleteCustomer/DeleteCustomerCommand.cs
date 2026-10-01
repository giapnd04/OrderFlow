using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.DeleteCustomer;

public sealed record DeleteCustomerCommand(int CustomerId) : ICommand<DeleteCustomerResult>;
