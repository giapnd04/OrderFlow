using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Products.GetProducts;

public sealed record GetProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    ProductStatus? Status = null) : IQuery<GetProductsResult>;
