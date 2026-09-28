using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.GetLowStockProducts;

public sealed record GetLowStockProductsQuery(int Threshold = 10, int Limit = 50)
    : IQuery<GetLowStockProductsResult>;
