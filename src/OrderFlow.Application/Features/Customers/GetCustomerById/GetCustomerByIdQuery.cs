using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(int CustomerId) : IQuery<GetCustomerByIdResult>;
