using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(string Name, string Email, string?Phone) : ICommand<CreateCustomerResult>;