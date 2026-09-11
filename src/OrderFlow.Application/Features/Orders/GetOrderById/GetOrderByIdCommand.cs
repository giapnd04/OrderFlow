using OrderFlow.Application.Abstractions.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderFlow.Application.Features.Orders.GetOrderById
{
    public sealed record GetOrderByIdCommand(int OrderId)
    : IQuery<GetOrderByIdResult>;
}
