using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Products.ReserveStock;

public sealed record ReserveStockCommand(int ProductId, int Quantity) : ICommand<ReserveStockResult>;
