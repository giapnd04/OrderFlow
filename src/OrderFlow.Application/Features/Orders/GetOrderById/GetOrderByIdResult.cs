using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderFlow.Application.Features.Orders.GetOrderById
{
    public sealed record GetOrderByIdResult(
        int OrderId,
        int CustomerId,
        string Status,
        decimal TotalAmount,
        IReadOnlyCollection<GetOrderByIdItemResult> Items);

    public sealed record GetOrderByIdItemResult(
        int ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal Subtotal);
}
