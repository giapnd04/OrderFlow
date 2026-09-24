using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.UpdateProduct;

public sealed record UpdateProductCommand(int ProductId, string Name, decimal Price)
    : ICommand<UpdateProductResult>;
