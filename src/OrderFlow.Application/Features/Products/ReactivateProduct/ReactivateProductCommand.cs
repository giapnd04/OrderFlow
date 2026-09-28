using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.ReactivateProduct;

public sealed record ReactivateProductCommand(int ProductId) : ICommand<ReactivateProductResult>;
