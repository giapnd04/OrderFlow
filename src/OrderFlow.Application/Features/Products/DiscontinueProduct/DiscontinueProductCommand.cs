using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.DiscontinueProduct;

public sealed record DiscontinueProductCommand(int ProductId) : ICommand<DiscontinueProductResult>;
