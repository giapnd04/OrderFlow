using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.ChangeCustomerEmail;

public sealed record ChangeCustomerEmailCommand(int CustomerId, string Email) : ICommand<ChangeCustomerEmailResult>;
