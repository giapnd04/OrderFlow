using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.RestockProduct;

public sealed record RestockProductCommand(int ProductId, int Quantity) : ICommand<RestockProductResult>;
