using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.UpdateCustomer;

public sealed record UpdateCustomerCommand(int CustomerId, string Name, string? Phone)
    : ICommand<UpdateCustomerResult>;
