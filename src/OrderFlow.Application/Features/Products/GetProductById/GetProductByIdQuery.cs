using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.GetProductById;

public sealed record GetProductByIdQuery(int ProductId) : IQuery<GetProductByIdResult>;
