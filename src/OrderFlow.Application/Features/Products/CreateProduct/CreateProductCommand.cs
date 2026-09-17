using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.CreateProduct;

public sealed record CreateProductCommand(string Sku,string Name,decimal Price,int StockQuantity) : ICommand<CreateProductResult>;